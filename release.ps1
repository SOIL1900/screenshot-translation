[CmdletBinding()]
param(
    [string]$NotesFile = "",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
$AppProjectPath = Join-Path $RepoRoot "src\ScreenshotTranslation.App\ScreenshotTranslation.App.csproj"
$InstallerSourcePath = Join-Path $RepoRoot "installer\Package.wxs"
$ReadmePath = Join-Path $RepoRoot "README.md"
$EnglishReadmePath = Join-Path $RepoRoot "README.en.md"
$ReleaseAssetName = "ScreenshotTranslation.Installer.msi"
$OfficialRepository = "SOIL1900/screenshot-translation"
$OfficialPublisher = "SOIL1900"

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Resolve-Executable {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [string[]]$FallbackPaths = @()
    )

    $Command = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $Command) {
        return $Command.Source
    }

    foreach ($FallbackPath in $FallbackPaths) {
        if (Test-Path -LiteralPath $FallbackPath -PathType Leaf) {
            return $FallbackPath
        }
    }

    throw "Required command not found: $Name"
}

function Invoke-External {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$CommandArguments,

        [switch]$AllowFailure
    )

    $PreviousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $RawOutput = @(& $FilePath @CommandArguments 2>&1)
        $ExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $PreviousErrorActionPreference
    }

    $OutputLines = @($RawOutput | ForEach-Object { $_.ToString() })
    $OutputText = $OutputLines -join [Environment]::NewLine
    if ($ExitCode -ne 0 -and -not $AllowFailure) {
        $CommandText = "$FilePath $($CommandArguments -join ' ')"
        if ([string]::IsNullOrWhiteSpace($OutputText)) {
            throw "$CommandText failed with exit code $ExitCode."
        }

        throw "$CommandText failed with exit code $ExitCode.`n$OutputText"
    }

    return [pscustomobject]@{
        ExitCode = $ExitCode
        OutputLines = $OutputLines
        OutputText = $OutputText
    }
}

function Get-RequiredJson {
    param(
        [Parameter(Mandatory)]
        [string]$GhPath,

        [Parameter(Mandatory)]
        [string[]]$CommandArguments
    )

    $Result = Invoke-External -FilePath $GhPath -CommandArguments $CommandArguments
    try {
        return $Result.OutputText | ConvertFrom-Json
    }
    catch {
        throw "GitHub CLI returned invalid JSON.`n$($Result.OutputText)"
    }
}

function Get-OptionalRelease {
    param(
        [Parameter(Mandatory)]
        [string]$GhPath,

        [Parameter(Mandatory)]
        [string]$Repository,

        [Parameter(Mandatory)]
        [string]$Tag
    )

    $Result = Invoke-External `
        -FilePath $GhPath `
        -CommandArguments @("api", "repos/$Repository/releases/tags/$Tag") `
        -AllowFailure

    if ($Result.ExitCode -eq 0) {
        return $Result.OutputText | ConvertFrom-Json
    }

    if ($Result.OutputText -match "(?i)not found|HTTP 404") {
        return $null
    }

    throw "Unable to query GitHub Release $Tag.`n$($Result.OutputText)"
}

function Enable-WindowsSystemProxy {
    if (-not [string]::IsNullOrWhiteSpace($env:HTTPS_PROXY)) {
        return
    }

    try {
        $InternetSettings = Get-ItemProperty `
            "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"
    }
    catch {
        return
    }

    if ($InternetSettings.ProxyEnable -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$InternetSettings.ProxyServer)) {
        return
    }

    $ProxyServer = [string]$InternetSettings.ProxyServer
    $ProxyAddress = $ProxyServer
    if ($ProxyServer.Contains("=")) {
        $ProxyMap = @{}
        foreach ($Pair in $ProxyServer.Split(";", [StringSplitOptions]::RemoveEmptyEntries)) {
            $Parts = $Pair.Split("=", 2)
            if ($Parts.Count -eq 2) {
                $ProxyMap[$Parts[0].Trim().ToLowerInvariant()] = $Parts[1].Trim()
            }
        }

        if ($ProxyMap.ContainsKey("https")) {
            $ProxyAddress = $ProxyMap["https"]
        }
        elseif ($ProxyMap.ContainsKey("http")) {
            $ProxyAddress = $ProxyMap["http"]
        }
    }

    if ($ProxyAddress -notmatch "^[a-z]+://") {
        $ProxyAddress = "http://$ProxyAddress"
    }

    $env:HTTPS_PROXY = $ProxyAddress
    if ([string]::IsNullOrWhiteSpace($env:HTTP_PROXY)) {
        $env:HTTP_PROXY = $ProxyAddress
    }

    Write-Host "Using the Windows system proxy for GitHub CLI: $ProxyAddress" -ForegroundColor DarkGray
}

function Get-ChangedPaths {
    param(
        [Parameter(Mandatory)]
        [string]$GitPath
    )

    $Result = Invoke-External `
        -FilePath $GitPath `
        -CommandArguments @("status", "--porcelain=v1", "--untracked-files=all")
    $Paths = @()
    foreach ($Line in $Result.OutputLines) {
        if ($Line.Length -lt 4) {
            continue
        }

        $Path = $Line.Substring(3).Trim().Trim('"').Replace("\", "/")
        $Paths += $Path
    }

    return @($Paths)
}

function Assert-OnlyAllowedChanges {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$ChangedPaths,

        [Parameter(Mandatory)]
        [string[]]$AllowedPaths
    )

    $UnexpectedPaths = @($ChangedPaths | Where-Object { $AllowedPaths -notcontains $_ })
    if ($UnexpectedPaths.Count -gt 0) {
        throw "The working tree contains unrelated changes:`n$($UnexpectedPaths -join [Environment]::NewLine)"
    }
}

function Get-UpdatedReadmeContent {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Version
    )

    $Content = [System.IO.File]::ReadAllText($Path)
    $VersionPattern = [regex]::new("Screenshot Translation [0-9]+\.[0-9]+\.[0-9]+")
    if ($VersionPattern.Matches($Content).Count -ne 1) {
        throw "Expected exactly one download version in $Path."
    }

    return $VersionPattern.Replace($Content, "Screenshot Translation $Version", 1)
}

function Get-LatestSourceFile {
    $SourceFiles = @(
        Get-ChildItem -LiteralPath (Join-Path $RepoRoot "src") -Recurse -File |
            Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" }
        Get-ChildItem -LiteralPath (Join-Path $RepoRoot "installer") -Recurse -File |
            Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" }
    )

    foreach ($RootFileName in @(
        "ScreenshotTranslation.sln",
        "Directory.Build.props",
        "Directory.Packages.props")) {
        $RootFile = Join-Path $RepoRoot $RootFileName
        if (Test-Path -LiteralPath $RootFile -PathType Leaf) {
            $SourceFiles += Get-Item -LiteralPath $RootFile
        }
    }

    return $SourceFiles | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
}

Enable-WindowsSystemProxy

$GitPath = Resolve-Executable -Name "git"
$GhFallback = Join-Path $env:ProgramFiles "GitHub CLI\gh.exe"
$GhPath = Resolve-Executable -Name "gh" -FallbackPaths @($GhFallback)

foreach ($RequiredPath in @(
    $AppProjectPath,
    $InstallerSourcePath,
    $ReadmePath,
    $EnglishReadmePath)) {
    if (-not (Test-Path -LiteralPath $RequiredPath -PathType Leaf)) {
        throw "Required project file not found: $RequiredPath"
    }
}

$AppProjectContent = [System.IO.File]::ReadAllText($AppProjectPath)
$InstallerSourceContent = [System.IO.File]::ReadAllText($InstallerSourcePath)
$AppVersionMatch = [regex]::Match(
    $AppProjectContent,
    '<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>')
$InstallerVersionMatch = [regex]::Match(
    $InstallerSourceContent,
    'Version="([0-9]+\.[0-9]+\.[0-9]+)(?:\.[0-9]+)?"')
if (-not $AppVersionMatch.Success -or -not $InstallerVersionMatch.Success) {
    throw "Unable to read the app and MSI versions."
}

$Version = $AppVersionMatch.Groups[1].Value
if ($InstallerVersionMatch.Groups[1].Value -ne $Version) {
    throw "App version $Version does not match MSI version $($InstallerVersionMatch.Groups[1].Value)."
}

$Tag = "v$Version"
$VersionedMsiPath = Join-Path $RepoRoot "artifacts\ScreenshotTranslation-$Version-x64.msi"
if (-not (Test-Path -LiteralPath $VersionedMsiPath -PathType Leaf)) {
    throw "Packaged MSI not found: $VersionedMsiPath`nRun package.ps1 first."
}

$MsiFile = Get-Item -LiteralPath $VersionedMsiPath
$LatestSourceFile = Get-LatestSourceFile
$StaleSourceFile = $null
if ($null -ne $LatestSourceFile -and
    $LatestSourceFile.LastWriteTimeUtc -gt $MsiFile.LastWriteTimeUtc) {
    $StaleSourceFile = $LatestSourceFile
}
if ($null -ne $StaleSourceFile -and -not $DryRun) {
    throw "The MSI is older than $($LatestSourceFile.FullName). Run package.ps1 again before releasing."
}

$MsiHash = Get-FileHash -LiteralPath $MsiFile.FullName -Algorithm SHA256
$ExpectedDigest = "sha256:$($MsiHash.Hash.ToLowerInvariant())"

$RepositoryRootResult = Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("rev-parse", "--show-toplevel")
$GitRoot = [System.IO.Path]::GetFullPath($RepositoryRootResult.OutputText.Trim())
if (-not $GitRoot.Equals(
        [System.IO.Path]::GetFullPath($RepoRoot),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "release.ps1 must be stored in the repository root."
}

$CurrentBranch = (Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("branch", "--show-current")).OutputText.Trim()
if ([string]::IsNullOrWhiteSpace($CurrentBranch)) {
    throw "The repository is in detached HEAD state."
}

$Upstream = (Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}")).OutputText.Trim()
$UpstreamSeparator = $Upstream.IndexOf('/')
if ($UpstreamSeparator -le 0) {
    throw "Unable to determine the tracked Git remote from $Upstream."
}

$RemoteName = $Upstream.Substring(0, $UpstreamSeparator)
$RemoteBranch = $Upstream.Substring($UpstreamSeparator + 1)
if ($CurrentBranch -ne $RemoteBranch) {
    throw "Current branch $CurrentBranch does not match tracked branch $RemoteBranch."
}

$ResolvedNotesFile = ""
if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    $NotesCandidate = $NotesFile
    if (-not [System.IO.Path]::IsPathRooted($NotesCandidate)) {
        $NotesCandidate = Join-Path $RepoRoot $NotesCandidate
    }

    if (-not (Test-Path -LiteralPath $NotesCandidate -PathType Leaf)) {
        throw "Release notes file not found: $NotesCandidate"
    }

    $ResolvedNotesFile = (Resolve-Path -LiteralPath $NotesCandidate).Path
}

$InitialAllowedChanges = @(
    "src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj",
    "installer/Package.wxs")
$InitialChanges = @(Get-ChangedPaths -GitPath $GitPath)
Assert-OnlyAllowedChanges `
    -ChangedPaths $InitialChanges `
    -AllowedPaths $InitialAllowedChanges

$null = Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("fetch", $RemoteName, $RemoteBranch, "--tags")
$DivergenceText = (Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("rev-list", "--left-right", "--count", "$Upstream...HEAD")).OutputText.Trim()
$Divergence = @($DivergenceText -split "\s+")
if ($Divergence.Count -ne 2) {
    throw "Unable to determine branch divergence from $DivergenceText."
}

$BehindCount = [int]$Divergence[0]
if ($BehindCount -gt 0) {
    throw "The current branch is behind $Upstream by $BehindCount commit(s). Pull before releasing."
}

$null = Invoke-External `
    -FilePath $GhPath `
    -CommandArguments @("auth", "status", "--hostname", "github.com")
$RepositoryInfo = Get-RequiredJson `
    -GhPath $GhPath `
    -CommandArguments @("repo", "view", "--json", "nameWithOwner,defaultBranchRef")
$Repository = [string]$RepositoryInfo.nameWithOwner
$DefaultBranch = [string]$RepositoryInfo.defaultBranchRef.name
if ($CurrentBranch -ne $DefaultBranch) {
    throw "Releases must be created from the default branch $DefaultBranch, not $CurrentBranch."
}

$AuthenticatedUser = Get-RequiredJson `
    -GhPath $GhPath `
    -CommandArguments @("api", "user")
$PublisherLogin = [string]$AuthenticatedUser.login
$IsOfficialPublisher = `
    $Repository.Equals($OfficialRepository, [StringComparison]::OrdinalIgnoreCase) -and
    $PublisherLogin.Equals($OfficialPublisher, [StringComparison]::OrdinalIgnoreCase)
if (-not $DryRun -and -not $IsOfficialPublisher) {
    throw "Official Releases can only be published to $OfficialRepository by GitHub account $OfficialPublisher. Current repository: $Repository. Current account: $PublisherLogin."
}

$ExistingRelease = Get-OptionalRelease `
    -GhPath $GhPath `
    -Repository $Repository `
    -Tag $Tag
if ($null -ne $ExistingRelease -and -not [bool]$ExistingRelease.draft) {
    Write-Host "Release $Tag is already published: $($ExistingRelease.html_url)" -ForegroundColor Green
    exit 0
}

$UpdatedReadmeContent = Get-UpdatedReadmeContent -Path $ReadmePath -Version $Version
$UpdatedEnglishReadmeContent = Get-UpdatedReadmeContent `
    -Path $EnglishReadmePath `
    -Version $Version
$ReadmeNeedsUpdate = $UpdatedReadmeContent -ne [System.IO.File]::ReadAllText($ReadmePath)
$EnglishReadmeNeedsUpdate = `
    $UpdatedEnglishReadmeContent -ne [System.IO.File]::ReadAllText($EnglishReadmePath)

Write-Host "Repository: $Repository" -ForegroundColor DarkGray
Write-Host "GitHub account: $PublisherLogin" -ForegroundColor DarkGray
Write-Host "Official publisher authorized: $IsOfficialPublisher" -ForegroundColor DarkGray
Write-Host "Branch: $CurrentBranch" -ForegroundColor DarkGray
Write-Host "Version: $Version" -ForegroundColor Cyan
Write-Host "Tag: $Tag" -ForegroundColor Cyan
Write-Host "Installer: $($MsiFile.FullName)" -ForegroundColor Cyan
Write-Host "Size: $($MsiFile.Length) bytes" -ForegroundColor Cyan
Write-Host "SHA-256: $($MsiHash.Hash)" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host ""
    if ($null -ne $StaleSourceFile) {
        throw "Dry run failed: the MSI is older than $($StaleSourceFile.FullName). Run package.ps1 again."
    }

    Write-Host "Dry run succeeded. No files, commits, pushes, tags, or Releases were changed." -ForegroundColor Green
    Write-Host "README update required: $($ReadmeNeedsUpdate -or $EnglishReadmeNeedsUpdate)" -ForegroundColor Green
    Write-Host "Existing draft will be resumed: $($null -ne $ExistingRelease)" -ForegroundColor Green
    exit 0
}

$ExpectedConfirmation = "RELEASE-$Tag"
Write-Host ""
Write-Host "This will push $CurrentBranch and publish $Tag to $Repository." -ForegroundColor Yellow
$Confirmation = Read-Host "Type $ExpectedConfirmation to continue"
if (-not $Confirmation.Equals($ExpectedConfirmation, [StringComparison]::Ordinal)) {
    throw "Release confirmation did not match. Nothing was published."
}

if ($ReadmeNeedsUpdate) {
    Write-Utf8NoBom -Path $ReadmePath -Content $UpdatedReadmeContent
}
if ($EnglishReadmeNeedsUpdate) {
    Write-Utf8NoBom -Path $EnglishReadmePath -Content $UpdatedEnglishReadmeContent
}

$ReleaseManagedPaths = @(
    "src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj",
    "installer/Package.wxs",
    "README.md",
    "README.en.md")
$ReleaseChanges = @(Get-ChangedPaths -GitPath $GitPath)
Assert-OnlyAllowedChanges `
    -ChangedPaths $ReleaseChanges `
    -AllowedPaths $ReleaseManagedPaths

if ($ReleaseChanges.Count -gt 0) {
    $null = Invoke-External `
        -FilePath $GitPath `
        -CommandArguments @(
            "add",
            "--",
            $AppProjectPath,
            $InstallerSourcePath,
            $ReadmePath,
            $EnglishReadmePath)
    $StagedFiles = (Invoke-External `
        -FilePath $GitPath `
        -CommandArguments @("diff", "--cached", "--name-only")).OutputLines
    if ($StagedFiles.Count -gt 0) {
        $null = Invoke-External `
            -FilePath $GitPath `
            -CommandArguments @("commit", "-m", "chore(release): $Tag")
    }
}

$null = Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("push", $RemoteName, $CurrentBranch)
$CommitSha = (Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("rev-parse", "HEAD")).OutputText.Trim()

if ($null -eq $ExistingRelease) {
    $CreateArguments = @(
        "release",
        "create",
        $Tag,
        "--repo",
        $Repository,
        "--target",
        $CommitSha,
        "--title",
        $Tag,
        "--draft")
    if ([string]::IsNullOrWhiteSpace($ResolvedNotesFile)) {
        $CreateArguments += @(
            "--generate-notes",
            "--notes",
            "Windows x64 self-contained MSI.`n`nSHA-256: ``$($MsiHash.Hash)``")
    }
    else {
        $CreateArguments += @("--notes-file", $ResolvedNotesFile)
    }

    $null = Invoke-External -FilePath $GhPath -CommandArguments $CreateArguments
    $ExistingRelease = Get-OptionalRelease `
        -GhPath $GhPath `
        -Repository $Repository `
        -Tag $Tag
    if ($null -eq $ExistingRelease) {
        throw "GitHub did not return the newly created draft Release."
    }
}
else {
    $EditDraftArguments = @(
        "release",
        "edit",
        $Tag,
        "--repo",
        $Repository,
        "--target",
        $CommitSha,
        "--draft")
    if (-not [string]::IsNullOrWhiteSpace($ResolvedNotesFile)) {
        $EditDraftArguments += @("--notes-file", $ResolvedNotesFile)
    }

    $null = Invoke-External -FilePath $GhPath -CommandArguments $EditDraftArguments
}

$ReleaseId = [long]$ExistingRelease.id
$Assets = Get-RequiredJson `
    -GhPath $GhPath `
    -CommandArguments @("api", "repos/$Repository/releases/$ReleaseId/assets")
$ExistingAsset = @($Assets | Where-Object { $_.name -eq $ReleaseAssetName }) |
    Select-Object -First 1
$ExistingAssetIsValid = $null -ne $ExistingAsset -and
    [string]$ExistingAsset.state -eq "uploaded" -and
    [long]$ExistingAsset.size -eq $MsiFile.Length -and
    ([string]::IsNullOrWhiteSpace([string]$ExistingAsset.digest) -or
        [string]$ExistingAsset.digest -eq $ExpectedDigest)

if (-not $ExistingAssetIsValid -and $null -ne $ExistingAsset) {
    $null = Invoke-External `
        -FilePath $GhPath `
        -CommandArguments @(
            "api",
            "--method",
            "DELETE",
            "repos/$Repository/releases/assets/$($ExistingAsset.id)")
}

$TemporaryReleaseDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "ScreenshotTranslation-$Tag-release-$PID"
$TemporaryUploadPath = Join-Path $TemporaryReleaseDirectory $ReleaseAssetName
try {
    if (-not $ExistingAssetIsValid) {
        New-Item -ItemType Directory -Path $TemporaryReleaseDirectory -Force | Out-Null
        Copy-Item `
            -LiteralPath $MsiFile.FullName `
            -Destination $TemporaryUploadPath `
            -Force
        Write-Host "Uploading $ReleaseAssetName..." -ForegroundColor Yellow
        $null = Invoke-External `
            -FilePath $GhPath `
            -CommandArguments @(
                "release",
                "upload",
                $Tag,
                $TemporaryUploadPath,
                "--repo",
                $Repository)
    }

    $VerifiedAssets = Get-RequiredJson `
        -GhPath $GhPath `
        -CommandArguments @("api", "repos/$Repository/releases/$ReleaseId/assets")
    $VerifiedAsset = @($VerifiedAssets | Where-Object { $_.name -eq $ReleaseAssetName }) |
        Select-Object -First 1
    if ($null -eq $VerifiedAsset -or
        [string]$VerifiedAsset.state -ne "uploaded" -or
        [long]$VerifiedAsset.size -ne $MsiFile.Length -or
        (-not [string]::IsNullOrWhiteSpace([string]$VerifiedAsset.digest) -and
            [string]$VerifiedAsset.digest -ne $ExpectedDigest)) {
        throw "The uploaded MSI failed remote size or SHA-256 verification. The Release remains a draft."
    }

    $null = Invoke-External `
        -FilePath $GhPath `
        -CommandArguments @(
            "release",
            "edit",
            $Tag,
            "--repo",
            $Repository,
            "--draft=false",
            "--latest")
}
finally {
    if (Test-Path -LiteralPath $TemporaryUploadPath -PathType Leaf) {
        Remove-Item -LiteralPath $TemporaryUploadPath -Force
    }
    if (Test-Path -LiteralPath $TemporaryReleaseDirectory -PathType Container) {
        Remove-Item -LiteralPath $TemporaryReleaseDirectory -Force
    }
}

$PublishedRelease = Get-OptionalRelease `
    -GhPath $GhPath `
    -Repository $Repository `
    -Tag $Tag
if ($null -eq $PublishedRelease -or [bool]$PublishedRelease.draft) {
    throw "Release $Tag was not published successfully."
}

$null = Invoke-External `
    -FilePath $GitPath `
    -CommandArguments @("fetch", $RemoteName, "tag", $Tag)

Write-Host ""
Write-Host "Release published successfully." -ForegroundColor Green
Write-Host "Release: $($PublishedRelease.html_url)" -ForegroundColor Green
Write-Host "Download: https://github.com/$Repository/releases/latest/download/$ReleaseAssetName" -ForegroundColor Green
Write-Host "SHA-256: $($MsiHash.Hash)" -ForegroundColor Green
