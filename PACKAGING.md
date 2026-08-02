# 本地打包说明

本文档用于在 Windows x64 环境中生成 Screenshot Translation 的自包含程序和 MSI 安装包。

## 准备环境

需要安装：

- Windows 10 或 Windows 11 x64
- .NET 8 SDK
- Git
- 可访问 NuGet 的网络连接

以下命令均在 PowerShell 中执行。

## 第一步：进入项目目录并设置版本号

复制并执行：

```powershell
$ErrorActionPreference = "Stop"
$Repo = "D:\software\app\screenshot-translation"
$Version = "1.0.9"

Set-Location $Repo
```

发布新版本时，只需要修改 `$Version`，例如：

```powershell
$Version = "1.0.10"
```

使用以下命令同步修改应用版本和 MSI 版本：

```powershell
$AppProjectPath = Join-Path $Repo "src\ScreenshotTranslation.App\ScreenshotTranslation.App.csproj"
$AppProjectContent = [System.IO.File]::ReadAllText($AppProjectPath)
$AppProjectContent = $AppProjectContent -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
[System.IO.File]::WriteAllText(
    $AppProjectPath,
    $AppProjectContent,
    [System.Text.UTF8Encoding]::new($false))

$InstallerSourcePath = Join-Path $Repo "installer\Package.wxs"
$InstallerSourceContent = [System.IO.File]::ReadAllText($InstallerSourcePath)
$InstallerSourceContent = $InstallerSourceContent -replace 'Version="[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?"', "Version=`"$Version`""
[System.IO.File]::WriteAllText(
    $InstallerSourcePath,
    $InstallerSourceContent,
    [System.Text.UTF8Encoding]::new($false))
```

## 第二步：清理旧的打包文件

复制并执行：

```powershell
Remove-Item (Join-Path $Repo "artifacts\publish") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Repo "installer\bin") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Repo "installer\obj") -Recurse -Force -ErrorAction SilentlyContinue
```

这些目录均为构建生成目录。清理后可以避免旧文件进入新的安装包。

## 第三步：恢复依赖

复制并执行：

```powershell
dotnet restore ScreenshotTranslation.sln
```

## 第四步：编译 Release 版本

复制并执行：

```powershell
dotnet build ScreenshotTranslation.sln -c Release --no-restore
```

## 第五步：运行自动化测试

复制并执行：

```powershell
dotnet test ScreenshotTranslation.sln -c Release --no-build
```

所有测试通过后再继续执行后续步骤。

## 第六步：生成自包含的 Windows x64 程序

复制并执行：

```powershell
dotnet publish `
    ".\src\ScreenshotTranslation.App\ScreenshotTranslation.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o ".\artifacts\publish"
```

生成的主程序位于：

```text
artifacts\publish\ScreenshotTranslation.exe
```

该发布目录包含 .NET 运行时，目标电脑不需要另外安装 .NET 8。

## 第七步：生成 MSI 安装包

必须先完成上一步的 `dotnet publish`，然后复制并执行：

```powershell
dotnet build `
    ".\installer\ScreenshotTranslation.Installer.wixproj" `
    -c Release
```

WiX 安装包生成在：

```text
installer\bin\x64\Release\ScreenshotTranslation.Installer.msi
```

## 第八步：复制并生成带版本号的安装包

复制并执行：

```powershell
$SourceMsi = Join-Path $Repo "installer\bin\x64\Release\ScreenshotTranslation.Installer.msi"
$OutputMsi = Join-Path $Repo "artifacts\ScreenshotTranslation-$Version-x64.msi"

Copy-Item $SourceMsi $OutputMsi -Force
```

## 第九步：显示安装包信息和 SHA-256

复制并执行：

```powershell
Get-Item $OutputMsi |
    Select-Object FullName, Length, LastWriteTime

Get-FileHash $OutputMsi -Algorithm SHA256
```

最终安装包位于：

```text
D:\software\app\screenshot-translation\artifacts\ScreenshotTranslation-版本号-x64.msi
```

例如：

```text
D:\software\app\screenshot-translation\artifacts\ScreenshotTranslation-1.0.9-x64.msi
```

## 完整连续执行版本

如果已经安装好所需环境，可以修改 `$Version` 后，按顺序复制执行下面的完整脚本：

```powershell
$ErrorActionPreference = "Stop"
$Repo = "D:\software\app\screenshot-translation"
$Version = "1.0.9"

Set-Location $Repo

$AppProjectPath = Join-Path $Repo "src\ScreenshotTranslation.App\ScreenshotTranslation.App.csproj"
$AppProjectContent = [System.IO.File]::ReadAllText($AppProjectPath)
$AppProjectContent = $AppProjectContent -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
[System.IO.File]::WriteAllText(
    $AppProjectPath,
    $AppProjectContent,
    [System.Text.UTF8Encoding]::new($false))

$InstallerSourcePath = Join-Path $Repo "installer\Package.wxs"
$InstallerSourceContent = [System.IO.File]::ReadAllText($InstallerSourcePath)
$InstallerSourceContent = $InstallerSourceContent -replace 'Version="[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?"', "Version=`"$Version`""
[System.IO.File]::WriteAllText(
    $InstallerSourcePath,
    $InstallerSourceContent,
    [System.Text.UTF8Encoding]::new($false))

Remove-Item (Join-Path $Repo "artifacts\publish") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Repo "installer\bin") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Repo "installer\obj") -Recurse -Force -ErrorAction SilentlyContinue

dotnet restore ScreenshotTranslation.sln
dotnet build ScreenshotTranslation.sln -c Release --no-restore
dotnet test ScreenshotTranslation.sln -c Release --no-build

dotnet publish `
    ".\src\ScreenshotTranslation.App\ScreenshotTranslation.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o ".\artifacts\publish"

dotnet build `
    ".\installer\ScreenshotTranslation.Installer.wixproj" `
    -c Release

$SourceMsi = Join-Path $Repo "installer\bin\x64\Release\ScreenshotTranslation.Installer.msi"
$OutputMsi = Join-Path $Repo "artifacts\ScreenshotTranslation-$Version-x64.msi"

Copy-Item $SourceMsi $OutputMsi -Force

Get-Item $OutputMsi |
    Select-Object FullName, Length, LastWriteTime

Get-FileHash $OutputMsi -Algorithm SHA256
```

执行完成后，`$OutputMsi` 指向最终可安装的 MSI 文件。
