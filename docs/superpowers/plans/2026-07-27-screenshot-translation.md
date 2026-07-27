# Screenshot Translation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个 Windows 10/11 x64 截图翻译桌面应用，通过全局快捷键冻结鼠标所在显示器、框选多模态翻译，并在同一结果面板完成快捷回复翻译和复制。

**Architecture:** 使用 .NET 8 + WPF 的单进程单实例架构。Core 项目保存不依赖 WPF/Win32 的配置、几何和翻译状态机；Infrastructure 项目封装 JSON 设置、OpenAI 兼容 HTTP、屏幕捕获与 Windows 原生能力；App 项目负责托盘、设置窗口和截图覆盖层。所有网络请求和屏幕捕获都经接口注入，核心行为使用 xUnit 测试。

**Tech Stack:** .NET 8, WPF, C# 12, System.Text.Json, HttpClient, xUnit, WiX Toolset 5, GitHub Actions

**Source of truth:** `docs/superpowers/specs/2026-07-27-screenshot-translation-design.md`

**Verification rule:** 每个任务只运行直接覆盖该任务风险的检查；跨模块组装完成后再运行一次全套测试，发布任务只进行一次 Release 构建、发布和 MSI 验证。小修不重复全量验证。

---

## File map

```text
ScreenshotTranslation.sln
Directory.Build.props
Directory.Packages.props
.editorconfig
.gitignore
LICENSE
README.md
src/
  ScreenshotTranslation.Core/
    Configuration/
      AppSettings.cs
      HotkeyGesture.cs
      SettingsValidator.cs
    Geometry/
      PixelPoint.cs
      PixelRect.cs
      ResizeHandle.cs
      SelectionGeometry.cs
      ResultPanelPlacement.cs
    Translation/
      ITranslationClient.cs
      LanguageCatalog.cs
      TranslationContracts.cs
      TranslationCoordinator.cs
      TranslationPrompts.cs
    Abstractions/
      IDiagnosticLog.cs
      ISettingsStore.cs
  ScreenshotTranslation.Infrastructure/
    Configuration/JsonSettingsStore.cs
    Diagnostics/FileDiagnosticLog.cs
    Translation/OpenAiRequestFactory.cs
    Translation/OpenAiResponseParser.cs
    Translation/OpenAiTranslationClient.cs
    Windows/GdiScreenCaptureService.cs
    Windows/PngCropService.cs
    Windows/MonitorService.cs
    Windows/GlobalHotkeyService.cs
    Windows/ForegroundWindowService.cs
    Windows/StartupRegistrationService.cs
    Windows/SingleInstanceCoordinator.cs
  ScreenshotTranslation.App/
    App.xaml
    App.xaml.cs
    app.manifest
    Composition/AppServices.cs
    Assets/AppIcon.ico
    Themes/Colors.Light.xaml
    Themes/Colors.Dark.xaml
    Themes/Controls.xaml
    Themes/Icons.xaml
    Services/TrayIconService.cs
    Services/ThemeService.cs
    Settings/SettingsWindow.xaml
    Settings/SettingsWindow.xaml.cs
    Settings/SettingsViewModel.cs
    Settings/GeneralSettingsView.xaml
    Settings/ModelSettingsView.xaml
    Settings/AboutView.xaml
    Settings/HotkeyRecorder.xaml
    Overlay/CaptureOverlayWindow.xaml
    Overlay/CaptureOverlayWindow.xaml.cs
    Overlay/OverlayViewModel.cs
    Overlay/SelectionCanvas.cs
    Overlay/TranslationPanelView.xaml
    Overlay/OverlayCoordinateMapper.cs
tests/
  ScreenshotTranslation.Core.Tests/
    Configuration/SettingsValidatorTests.cs
    Geometry/SelectionGeometryTests.cs
    Geometry/ResultPanelPlacementTests.cs
    Translation/TranslationCoordinatorTests.cs
  ScreenshotTranslation.Infrastructure.Tests/
    Configuration/JsonSettingsStoreTests.cs
    Diagnostics/FileDiagnosticLogTests.cs
    Translation/OpenAiRequestFactoryTests.cs
    Translation/OpenAiResponseParserTests.cs
    Translation/OpenAiTranslationClientTests.cs
    Windows/PngCropServiceTests.cs
  ScreenshotTranslation.App.Tests/
    Settings/SettingsViewModelTests.cs
    Overlay/OverlayViewModelTests.cs
installer/
  ScreenshotTranslation.Installer.wixproj
  Package.wxs
tools/
  Generate-AppIcon.ps1
.github/workflows/windows.yml
```

## Task 1: Install the SDK and scaffold the solution

**Files:**
- Create: `ScreenshotTranslation.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: project files under `src/` and `tests/`

- [ ] **Step 1: Install .NET 8 SDK because the current machine has no SDK**

Run:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --exact --source winget --accept-package-agreements --accept-source-agreements
dotnet --version
```

Expected: `dotnet --version` prints an `8.0.x` SDK version. If winget reports the package is already installed, reopen the terminal once and run only `dotnet --version`.

- [ ] **Step 2: Create the solution and projects**

Run:

```powershell
dotnet new sln -n ScreenshotTranslation
dotnet new classlib -n ScreenshotTranslation.Core -o src/ScreenshotTranslation.Core -f net8.0
dotnet new classlib -n ScreenshotTranslation.Infrastructure -o src/ScreenshotTranslation.Infrastructure -f net8.0
dotnet new wpf -n ScreenshotTranslation.App -o src/ScreenshotTranslation.App -f net8.0
dotnet new xunit -n ScreenshotTranslation.Core.Tests -o tests/ScreenshotTranslation.Core.Tests -f net8.0
dotnet new xunit -n ScreenshotTranslation.Infrastructure.Tests -o tests/ScreenshotTranslation.Infrastructure.Tests -f net8.0
dotnet new xunit -n ScreenshotTranslation.App.Tests -o tests/ScreenshotTranslation.App.Tests -f net8.0
dotnet sln ScreenshotTranslation.sln add src/ScreenshotTranslation.Core/ScreenshotTranslation.Core.csproj
dotnet sln ScreenshotTranslation.sln add src/ScreenshotTranslation.Infrastructure/ScreenshotTranslation.Infrastructure.csproj
dotnet sln ScreenshotTranslation.sln add src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj
dotnet sln ScreenshotTranslation.sln add tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj
dotnet sln ScreenshotTranslation.sln add tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj
dotnet sln ScreenshotTranslation.sln add tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj
dotnet add src/ScreenshotTranslation.Infrastructure/ScreenshotTranslation.Infrastructure.csproj reference src/ScreenshotTranslation.Core/ScreenshotTranslation.Core.csproj
dotnet add src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj reference src/ScreenshotTranslation.Core/ScreenshotTranslation.Core.csproj src/ScreenshotTranslation.Infrastructure/ScreenshotTranslation.Infrastructure.csproj
dotnet add tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj reference src/ScreenshotTranslation.Core/ScreenshotTranslation.Core.csproj
dotnet add tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj reference src/ScreenshotTranslation.Core/ScreenshotTranslation.Core.csproj src/ScreenshotTranslation.Infrastructure/ScreenshotTranslation.Infrastructure.csproj
dotnet add tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj reference src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj
```

Expected: all six projects are listed by `dotnet sln ScreenshotTranslation.sln list`.

- [ ] **Step 3: Add repository-wide compiler and package settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props` and switch each test project to the listed central versions:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>
</Project>
```

Update the WPF project properties:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <UseWindowsForms>true</UseWindowsForms>
  <ApplicationManifest>app.manifest</ApplicationManifest>
  <AssemblyName>ScreenshotTranslation</AssemblyName>
  <RootNamespace>ScreenshotTranslation.App</RootNamespace>
</PropertyGroup>
```

Change `ScreenshotTranslation.Infrastructure`, `ScreenshotTranslation.Infrastructure.Tests`, and `ScreenshotTranslation.App.Tests` to `net8.0-windows`. Set `<UseWindowsForms>true</UseWindowsForms>` in the infrastructure projects and `<UseWPF>true</UseWPF>` plus `<UseWindowsForms>true</UseWindowsForms>` in the app test project.

Create `.editorconfig`:

```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,xaml}]
indent_style = space
indent_size = 4

[*.{xml,props,targets,wxs,wixproj,yml,yaml,json,md}]
indent_style = space
indent_size = 2
```

Create `.gitignore`:

```gitignore
.vs/
bin/
obj/
artifacts/
TestResults/
*.user
*.suo
.env
.env.*
settings.json
settings.json.tmp
settings.corrupt-*.json
```

Create `LICENSE`:

```text
MIT License

Copyright (c) 2026 Screenshot Translation contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 4: Add per-monitor DPI awareness**

Create `src/ScreenshotTranslation.App/app.manifest` with these compatibility settings:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
      <supportedOS Id="{4f476546-937d-4f4e-a1c0-85c0f6398caa}" />
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 5: Verify the empty scaffold once**

Run:

```powershell
dotnet restore ScreenshotTranslation.sln
dotnet build ScreenshotTranslation.sln -c Debug --no-restore
dotnet test ScreenshotTranslation.sln -c Debug --no-build
```

Expected: build and the three generated test projects pass.

- [ ] **Step 6: Commit**

```powershell
git add ScreenshotTranslation.sln Directory.Build.props Directory.Packages.props .editorconfig .gitignore LICENSE src tests
git commit -m "build: scaffold WPF solution"
```

## Task 2: Define settings, hotkey defaults, and validation

**Files:**
- Create: `src/ScreenshotTranslation.Core/Configuration/AppSettings.cs`
- Create: `src/ScreenshotTranslation.Core/Configuration/HotkeyGesture.cs`
- Create: `src/ScreenshotTranslation.Core/Configuration/SettingsValidator.cs`
- Create: `tests/ScreenshotTranslation.Core.Tests/Configuration/SettingsValidatorTests.cs`
- Delete: generated `Class1.cs` files

- [ ] **Step 1: Write failing tests for defaults and validation**

Create tests covering the exact product defaults and invalid fields:

```csharp
using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.Core.Tests.Configuration;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void Defaults_match_the_approved_spec()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1", settings.Model.BaseUrl);
        Assert.Equal("qwen3.7-flash", settings.Model.ModelName);
        Assert.False(settings.Model.EnableThinking);
        Assert.Equal("zh-CN", settings.General.DefaultTargetLanguage);
        Assert.Equal(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44), settings.General.CaptureHotkey);
    }

    [Fact]
    public void Validate_reports_each_recoverable_field_error()
    {
        var settings = AppSettings.CreateDefault() with
        {
            Model = AppSettings.CreateDefault().Model with
            {
                BaseUrl = "not-a-url",
                ApiKey = "",
                ModelName = "",
                Temperature = 3,
                MaxOutputTokens = 1,
                RequestTimeoutSeconds = 1,
                ExtraParametersJson = "[]"
            }
        };

        var fields = SettingsValidator.Validate(settings).Select(issue => issue.Field).ToHashSet();

        Assert.Contains("Model.BaseUrl", fields);
        Assert.Contains("Model.ApiKey", fields);
        Assert.Contains("Model.ModelName", fields);
        Assert.Contains("Model.Temperature", fields);
        Assert.Contains("Model.MaxOutputTokens", fields);
        Assert.Contains("Model.RequestTimeoutSeconds", fields);
        Assert.Contains("Model.ExtraParametersJson", fields);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj --filter SettingsValidatorTests
```

Expected: FAIL because the configuration types do not exist.

- [ ] **Step 3: Implement immutable settings records**

Use these public contracts:

```csharp
namespace ScreenshotTranslation.Core.Configuration;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44);
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed record GeneralSettings(
    HotkeyGesture CaptureHotkey,
    string DefaultTargetLanguage,
    bool RunAtStartup,
    AppTheme Theme);

public sealed record ModelSettings(
    string BaseUrl,
    string ApiKey,
    string ModelName,
    bool EnableThinking,
    double Temperature,
    int MaxOutputTokens,
    int RequestTimeoutSeconds,
    string ExtraParametersJson);

public sealed record AppSettings(GeneralSettings General, ModelSettings Model)
{
    public static AppSettings CreateDefault() => new(
        new GeneralSettings(HotkeyGesture.Default, "zh-CN", false, AppTheme.System),
        new ModelSettings(
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            string.Empty,
            "qwen3.7-flash",
            false,
            0.2,
            2048,
            30,
            "{}"));
}
```

Implement `SettingsValidator.Validate(AppSettings)` to return `ValidationIssue(Field, Message)` records. Require an absolute HTTP/HTTPS URL, non-empty API Key/model, temperature `0..2`, output tokens `64..8192`, timeout `5..120`, an object-shaped extra-parameters JSON value, and at least one hotkey modifier plus a non-modifier virtual key.

- [ ] **Step 4: Run the focused tests**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj --filter SettingsValidatorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenshotTranslation.Core tests/ScreenshotTranslation.Core.Tests
git commit -m "feat: define validated application settings"
```

## Task 3: Persist settings atomically and write content-free diagnostics

**Files:**
- Create: `src/ScreenshotTranslation.Core/Abstractions/ISettingsStore.cs`
- Create: `src/ScreenshotTranslation.Core/Abstractions/IDiagnosticLog.cs`
- Create: `src/ScreenshotTranslation.Infrastructure/Configuration/JsonSettingsStore.cs`
- Create: `src/ScreenshotTranslation.Infrastructure/Diagnostics/FileDiagnosticLog.cs`
- Create: `tests/ScreenshotTranslation.Infrastructure.Tests/Configuration/JsonSettingsStoreTests.cs`
- Create: `tests/ScreenshotTranslation.Infrastructure.Tests/Diagnostics/FileDiagnosticLogTests.cs`

- [ ] **Step 1: Write failing persistence and redaction tests**

The tests must prove round-trip persistence, corrupt-file backup, and absence of content:

```csharp
[Fact]
public async Task Save_and_load_round_trip_plaintext_api_key()
{
    using var directory = new TemporaryDirectory();
    var store = new JsonSettingsStore(directory.Path, TimeProvider.System);
    var expected = AppSettings.CreateDefault() with
    {
        Model = AppSettings.CreateDefault().Model with { ApiKey = "sk-personal-value" }
    };

    await store.SaveAsync(expected, CancellationToken.None);
    var actual = await store.LoadAsync(CancellationToken.None);

    Assert.Equal(expected, actual);
    Assert.Contains("sk-personal-value", await File.ReadAllTextAsync(store.SettingsPath));
}

[Fact]
public async Task Diagnostic_log_never_writes_user_content()
{
    using var directory = new TemporaryDirectory();
    var log = new FileDiagnosticLog(directory.Path, TimeProvider.System);

    await log.WriteAsync("translation_failed", new InvalidOperationException("sk-secret translated text"));
    var text = await File.ReadAllTextAsync(log.LogPath);

    Assert.Contains("InvalidOperationException", text);
    Assert.DoesNotContain("sk-secret", text);
    Assert.DoesNotContain("translated text", text);
}
```

Add a small disposable `TemporaryDirectory` test helper in the infrastructure test project.

- [ ] **Step 2: Run the focused tests to verify failure**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj --filter "JsonSettingsStoreTests|FileDiagnosticLogTests"
```

Expected: FAIL because the stores are missing.

- [ ] **Step 3: Implement the storage contracts and JSON store**

Use these interfaces:

```csharp
public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public interface IDiagnosticLog
{
    Task WriteAsync(string eventName, Exception? exception = null, CancellationToken cancellationToken = default);
}
```

`JsonSettingsStore` must accept a base directory for tests, expose `SettingsPath`, serialize with camel-case and indentation, write `settings.json.tmp`, and then use `File.Replace` or `File.Move` for atomic replacement. On `JsonException`, move the invalid file to a name such as `settings.corrupt-20260727-231530123.json` using UTC format `yyyyMMdd-HHmmssfff`, and return `AppSettings.CreateDefault()`.

`FileDiagnosticLog` must append JSON Lines containing only UTC timestamp, allow-listed event name, exception type, and `HResult`. Do not serialize `Exception.Message`, stack frames with arguments, settings, request bodies, response bodies, screenshot bytes, input text, or translation text.

- [ ] **Step 4: Run the focused persistence tests**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj --filter "JsonSettingsStoreTests|FileDiagnosticLogTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenshotTranslation.Core/Abstractions src/ScreenshotTranslation.Infrastructure/Configuration src/ScreenshotTranslation.Infrastructure/Diagnostics tests/ScreenshotTranslation.Infrastructure.Tests
git commit -m "feat: persist local settings safely"
```

## Task 4: Implement selection geometry and result-panel placement

**Files:**
- Create: `src/ScreenshotTranslation.Core/Geometry/PixelPoint.cs`
- Create: `src/ScreenshotTranslation.Core/Geometry/PixelRect.cs`
- Create: `src/ScreenshotTranslation.Core/Geometry/ResizeHandle.cs`
- Create: `src/ScreenshotTranslation.Core/Geometry/SelectionGeometry.cs`
- Create: `src/ScreenshotTranslation.Core/Geometry/ResultPanelPlacement.cs`
- Create: `tests/ScreenshotTranslation.Core.Tests/Geometry/SelectionGeometryTests.cs`
- Create: `tests/ScreenshotTranslation.Core.Tests/Geometry/ResultPanelPlacementTests.cs`

- [ ] **Step 1: Write failing geometry tests**

Cover normalization, bounds, movement, all eight handles, minimum size, and panel flipping:

```csharp
[Theory]
[InlineData(ResizeHandle.TopLeft, -10, -20, 90, 80, 110, 120)]
[InlineData(ResizeHandle.Top, 0, -20, 100, 80, 100, 120)]
[InlineData(ResizeHandle.TopRight, 10, -20, 100, 80, 110, 120)]
[InlineData(ResizeHandle.Right, 10, 0, 100, 100, 110, 100)]
[InlineData(ResizeHandle.BottomRight, 10, 20, 100, 100, 110, 120)]
[InlineData(ResizeHandle.Bottom, 0, 20, 100, 100, 100, 120)]
[InlineData(ResizeHandle.BottomLeft, -10, 20, 90, 100, 110, 120)]
[InlineData(ResizeHandle.Left, -10, 0, 90, 100, 110, 100)]
public void Resize_updates_the_requested_edges(
    ResizeHandle handle,
    int dx,
    int dy,
    int expectedX,
    int expectedY,
    int expectedWidth,
    int expectedHeight)
{
    var bounds = new PixelRect(0, 0, 1000, 800);
    var actual = SelectionGeometry.Resize(new PixelRect(100, 100, 100, 100), handle, dx, dy, bounds, 24);

    Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
}

[Fact]
public void Panel_flips_above_and_stays_inside_the_screen()
{
    var screen = new PixelRect(0, 0, 1920, 1080);
    var selection = new PixelRect(1600, 900, 300, 150);

    var result = ResultPanelPlacement.Place(selection, 520, 260, screen, 12);

    Assert.True(result.Bottom <= selection.Top);
    Assert.True(result.Right <= screen.Right);
}
```

- [ ] **Step 2: Verify focused test failure**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj --filter "SelectionGeometryTests|ResultPanelPlacementTests"
```

Expected: FAIL because geometry types do not exist.

- [ ] **Step 3: Implement integer physical-pixel geometry**

Use immutable value types:

```csharp
public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool Contains(PixelPoint point) => point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
}

public enum ResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}
```

`SelectionGeometry.Create` normalizes drag direction and clips to screen bounds. `Move` preserves size while clamping the rectangle. `Resize` changes only edges represented by the selected handle, enforces a 24-pixel minimum, and clamps to bounds. `ResultPanelPlacement.Place` tries below, then above, and finally clamps both axes to the screen; it never overlaps the selection when one side has sufficient room.

- [ ] **Step 4: Run the geometry tests**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj --filter "SelectionGeometryTests|ResultPanelPlacementTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenshotTranslation.Core/Geometry tests/ScreenshotTranslation.Core.Tests/Geometry
git commit -m "feat: add bounded selection geometry"
```

## Task 5: Build the OpenAI-compatible multimodal client

**Files:**
- Create: `src/ScreenshotTranslation.Core/Translation/ITranslationClient.cs`
- Create: `src/ScreenshotTranslation.Core/Translation/LanguageCatalog.cs`
- Create: `src/ScreenshotTranslation.Core/Translation/TranslationContracts.cs`
- Create: `src/ScreenshotTranslation.Core/Translation/TranslationPrompts.cs`
- Create: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiRequestFactory.cs`
- Create: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiResponseParser.cs`
- Create: `src/ScreenshotTranslation.Infrastructure/Translation/OpenAiTranslationClient.cs`
- Create: tests under `tests/ScreenshotTranslation.Infrastructure.Tests/Translation/`

- [ ] **Step 1: Write failing request and parser tests**

The tests must assert the exact model and thinking behavior:

```csharp
[Fact]
public void Screenshot_request_contains_image_target_language_and_disabled_thinking()
{
    var settings = AppSettings.CreateDefault().Model with { ApiKey = "sk-test" };

    var json = OpenAiRequestFactory.CreateScreenshotRequest(
        settings,
        [0x89, 0x50, 0x4E, 0x47],
        "zh-CN");

    Assert.Equal("qwen3.7-flash", json["model"]!.GetValue<string>());
    Assert.False(json["enable_thinking"]!.GetValue<bool>());
    Assert.StartsWith("data:image/png;base64,", json["messages"]![0]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>());
    Assert.Contains("zh-CN", json["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>());
}

[Theory]
[InlineData("{\"status\":\"ok\",\"sourceLanguage\":\"English\",\"sourceLanguageCode\":\"en\",\"translation\":\"你好\"}")]
[InlineData("```json\n{\"status\":\"ok\",\"sourceLanguage\":\"English\",\"sourceLanguageCode\":\"en\",\"translation\":\"你好\"}\n```")]
public void Parser_accepts_plain_and_fenced_json(string content)
{
    var result = OpenAiResponseParser.ParseScreenshotContent(content);

    Assert.Equal(TranslationResultStatus.Ok, result.Status);
    Assert.Equal("en", result.SourceLanguageCode);
    Assert.Equal("你好", result.Translation);
}
```

Also test `no_text`, HTTP `401/403/429/5xx`, timeout, missing choices, fallback pure text, and rejection of extra JSON keys that attempt to overwrite `model`, `messages`, `stream`, or `enable_thinking`.

- [ ] **Step 2: Run the translation tests to verify failure**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj --filter "OpenAiRequestFactoryTests|OpenAiResponseParserTests|OpenAiTranslationClientTests"
```

Expected: FAIL because the client is missing.

- [ ] **Step 3: Add translation contracts and hard-coded prompts**

Use these contracts:

```csharp
public enum TranslationResultStatus { Ok, NoText }
public enum TranslationErrorCode { Unauthorized, RateLimited, Timeout, ServiceUnavailable, InvalidResponse, Network }

public sealed record ScreenshotTranslationResult(
    TranslationResultStatus Status,
    string SourceLanguage,
    string SourceLanguageCode,
    string Translation);

public sealed record ReplyTranslationResult(string TargetLanguageCode, string Translation);

public sealed class TranslationClientException(TranslationErrorCode code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public TranslationErrorCode Code { get; } = code;
}

public interface ITranslationClient
{
    Task<ScreenshotTranslationResult> TranslateScreenshotAsync(
        ReadOnlyMemory<byte> pngBytes,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken);

    Task<ReplyTranslationResult> TranslateReplyAsync(
        string input,
        string targetLanguageCode,
        ModelSettings settings,
        CancellationToken cancellationToken);

    Task TestConnectionAsync(ModelSettings settings, CancellationToken cancellationToken);
}
```

`TranslationPrompts` must include the approved rules: detect the main language, translate all readable chat, preserve usernames/order/line breaks, understand game slang, return no explanations, and emit JSON with `status`, `sourceLanguage`, `sourceLanguageCode`, and `translation`. The reply prompt requests concise natural game-chat language and returns only the translation.

`LanguageCatalog` exposes immutable `LanguageOption(Code, DisplayName, PromptName)` entries for simplified Chinese, English, Japanese, Korean, Russian, French, German, Spanish, Portuguese, Italian, Thai, Vietnamese, Indonesian, Turkish, and Arabic. UI combo boxes bind to this single catalog rather than duplicating language strings.

- [ ] **Step 4: Implement request building, HTTP mapping, and parsing**

`OpenAiRequestFactory` must build `/chat/completions` requests with `stream: false`, configured `model`, `temperature`, `max_tokens`, and `enable_thinking`. Merge only non-reserved extra parameters from the configured object. Encode screenshots as PNG data URLs.

`OpenAiTranslationClient` must use an injected `HttpClient`, set `Authorization` to `Bearer {settings.ApiKey}` per request without modifying global default headers, apply the configured timeout through a linked cancellation token, and map status/error classes to `TranslationClientException`. Parse only `choices[0].message.content`; never display `reasoning_content`.

`TestConnectionAsync` sends a minimal text request asking the configured model to return `OK`. It succeeds only when a non-empty assistant message is returned.

- [ ] **Step 5: Run the focused model-client tests**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj --filter "OpenAiRequestFactoryTests|OpenAiResponseParserTests|OpenAiTranslationClientTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenshotTranslation.Core/Translation src/ScreenshotTranslation.Infrastructure/Translation tests/ScreenshotTranslation.Infrastructure.Tests/Translation
git commit -m "feat: add multimodal translation client"
```

## Task 6: Add latest-request-wins translation coordination

**Files:**
- Create: `src/ScreenshotTranslation.Core/Translation/TranslationCoordinator.cs`
- Create: `tests/ScreenshotTranslation.Core.Tests/Translation/TranslationCoordinatorTests.cs`

- [ ] **Step 1: Write failing concurrency tests**

Use a controllable fake client to complete requests out of order:

```csharp
[Fact]
public async Task New_screenshot_request_cancels_and_supersedes_the_old_request()
{
    var client = new ControllableTranslationClient();
    var coordinator = new TranslationCoordinator(client);
    var settings = AppSettings.CreateDefault().Model with { ApiKey = "sk-test" };

    var first = coordinator.TranslateScreenshotAsync([1], "zh-CN", settings, CancellationToken.None);
    var second = coordinator.TranslateScreenshotAsync([2], "zh-CN", settings, CancellationToken.None);

    client.CompleteScreenshot(1, new ScreenshotTranslationResult(TranslationResultStatus.Ok, "French", "fr", "旧结果"));
    client.CompleteScreenshot(2, new ScreenshotTranslationResult(TranslationResultStatus.Ok, "English", "en", "新结果"));

    Assert.Null(await first);
    Assert.Equal("新结果", (await second)!.Translation);
}
```

Define `ControllableTranslationClient` as a private test fake implementing all three `ITranslationClient` methods. Store screenshot `TaskCompletionSource` instances by the first PNG byte, complete them through `CompleteScreenshot`, make `TranslateReplyAsync` return a configured reply, and make `TestConnectionAsync` complete successfully. Also test explicit `Cancel`, error-state mapping, and reply requests using the most recent detected language.

- [ ] **Step 2: Verify the focused test fails**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj --filter TranslationCoordinatorTests
```

Expected: FAIL because the coordinator is missing.

- [ ] **Step 3: Implement the coordinator**

`TranslationCoordinator` owns a lock, monotonically increasing request version, and one active `CancellationTokenSource`. Starting a request increments the version and cancels/disposes the previous source. A completed result is returned only when its captured version equals the current version; cancellation caused by supersession returns `null`, while caller cancellation propagates. `Cancel()` increments the version and cancels the active source.

Keep presentation state out of the coordinator. UI view models translate domain errors into localized status text.

- [ ] **Step 4: Run the coordinator tests**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Core.Tests/ScreenshotTranslation.Core.Tests.csproj --filter TranslationCoordinatorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenshotTranslation.Core/Translation/TranslationCoordinator.cs tests/ScreenshotTranslation.Core.Tests/Translation
git commit -m "feat: coordinate latest translation request"
```

## Task 7: Implement Windows capture, crop, hotkey, focus, startup, and single-instance adapters

**Files:**
- Create: files under `src/ScreenshotTranslation.Infrastructure/Windows/`
- Create: `tests/ScreenshotTranslation.Infrastructure.Tests/Windows/PngCropServiceTests.cs`

- [ ] **Step 1: Write a failing pixel-crop test**

Generate a 100×100 bitmap with four colored quadrants, encode it as PNG, crop `PixelRect(50, 0, 50, 50)`, and assert the output is 50×50 with the expected top-right color. Include a second test that rejects a rectangle outside the captured frame.

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj --filter PngCropServiceTests
```

Expected: FAIL because `PngCropService` does not exist.

- [ ] **Step 2: Implement monitor capture and crop**

Define these infrastructure records/interfaces:

```csharp
public sealed record MonitorBounds(nint Handle, PixelRect PhysicalBounds);
public sealed record CapturedMonitorFrame(MonitorBounds Monitor, byte[] PngBytes);

public interface IMonitorService
{
    MonitorBounds GetMonitorUnderCursor();
}

public interface IScreenCaptureService
{
    CapturedMonitorFrame Capture(MonitorBounds monitor);
}
```

`MonitorService` uses `GetCursorPos`, `MonitorFromPoint(MONITOR_DEFAULTTONEAREST)`, and `GetMonitorInfo`. `GdiScreenCaptureService` creates a 32-bit bitmap of the physical monitor bounds and calls `Graphics.CopyFromScreen(..., CopyPixelOperation.SourceCopy)`. `PngCropService` validates and crops only the immutable captured PNG, then re-encodes PNG without resampling.

- [ ] **Step 3: Implement global hotkey and foreground restoration**

`GlobalHotkeyService` owns a message-only `HwndSource`, calls `RegisterHotKey`, raises `Pressed`, and unregisters during disposal. Map `HotkeyModifiers` to Win32 modifier flags and include `MOD_NOREPEAT`. Its `TryRegister` method returns a user-facing conflict result instead of throwing.

`ForegroundWindowService` captures `GetForegroundWindow()` immediately before showing the overlay and restores it with `ShowWindow(SW_RESTORE)` and `SetForegroundWindow()` after the overlay closes.

- [ ] **Step 4: Implement startup and single-instance coordination**

`StartupRegistrationService` manages only the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value named `ScreenshotTranslation`.

`SingleInstanceCoordinator` uses a named mutex `ScreenshotTranslation.SingleInstance` and a named pipe `ScreenshotTranslation.Activation`. The primary instance listens for the exact command `SHOW_SETTINGS`; a secondary instance sends that command and exits. The listener dispatches the activation callback onto the WPF dispatcher.

- [ ] **Step 5: Run only the crop test and build the infrastructure project**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.Infrastructure.Tests/ScreenshotTranslation.Infrastructure.Tests.csproj --filter PngCropServiceTests
dotnet build src/ScreenshotTranslation.Infrastructure/ScreenshotTranslation.Infrastructure.csproj -c Debug
```

Expected: crop tests and build pass. Do not automate real global-hotkey or foreground-focus assertions in CI because they require an interactive desktop.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenshotTranslation.Infrastructure/Windows tests/ScreenshotTranslation.Infrastructure.Tests/Windows
git commit -m "feat: add Windows desktop adapters"
```

## Task 8: Build the semantic theme and settings experience

**Files:**
- Create: theme files under `src/ScreenshotTranslation.App/Themes/`
- Create: settings files under `src/ScreenshotTranslation.App/Settings/`
- Create: `src/ScreenshotTranslation.App/Services/ThemeService.cs`
- Create: `tests/ScreenshotTranslation.App.Tests/Settings/SettingsViewModelTests.cs`

- [ ] **Step 1: Write failing settings view-model tests**

Test these behaviors with fake settings, hotkey, startup, and translation services:

```csharp
[Fact]
public async Task Save_keeps_plaintext_api_key_and_applies_the_new_hotkey()
{
    var fixture = SettingsViewModelFixture.Create();
    fixture.ViewModel.ApiKey = "sk-visible-value";
    fixture.ViewModel.CaptureHotkey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44);

    await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

    Assert.Equal("sk-visible-value", fixture.SettingsStore.Saved!.Model.ApiKey);
    Assert.Equal(0x44, fixture.HotkeyService.Registered!.VirtualKey);
}

[Fact]
public async Task Save_rolls_back_when_the_new_hotkey_conflicts()
{
    var fixture = SettingsViewModelFixture.Create(hotkeyRegistrationSucceeds: false);

    await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

    Assert.Equal("该快捷键已被其他程序占用", fixture.ViewModel.Errors["General.CaptureHotkey"]);
    Assert.Null(fixture.SettingsStore.Saved);
}
```

Also test field-level model validation, disabled save when unchanged, connection-test loading/success/error states, theme application, and startup registration.

Define `SettingsViewModelFixture` in the test file. It constructs `SettingsViewModel` with in-memory fakes for `ISettingsStore`, hotkey registration, startup registration, theme application, and `ITranslationClient`; the fakes expose only the last saved/registered/applied value needed by these assertions.

- [ ] **Step 2: Verify tests fail**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj --filter SettingsViewModelTests
```

Expected: FAIL because the settings UI layer is missing.

- [ ] **Step 3: Add semantic WPF resources**

Create light and dark dictionaries with semantic keys rather than view-local hex values. Use this approved palette:

```xml
<Color x:Key="Color.Primary">#7C3AED</Color>
<Color x:Key="Color.PrimaryHover">#6D28D9</Color>
<Color x:Key="Color.Focus">#8B5CF6</Color>
<Color x:Key="Color.Error">#DC2626</Color>
<Color x:Key="Color.Success">#15803D</Color>
```

Light resources use `#F6F7FB` background, `#FFFFFF` surface, `#171A24` primary text, `#5B6170` secondary text, and `#D8DCE6` border. Dark resources use `#0F0F17` background, `#1B1B27` surface, `#F1F5F9` primary text, `#CBD5E1` secondary text, and `#4C1D95` border.

`Controls.xaml` defines Segoe UI Variable typography, 8-pixel spacing tokens, 36-pixel minimum desktop button height, visible 2-pixel focus rings, non-shifting hover/pressed states, inline validation text, and disabled opacity. `Icons.xaml` contains reusable `Geometry` resources with one consistent outline style; do not use Emoji, CRT scanlines, neon glow, or decorative motion.

- [ ] **Step 4: Implement the settings window and hotkey recorder**

Build an `820 × 600` window with left navigation for 常规/模型/关于 and one right content region. Use visible labels for every field. Model settings use a normal WPF `TextBox` for API Key, not `PasswordBox`, so the value is always visible.

Place advanced model fields and extra JSON under an expanded “高级参数” section while keeping them directly accessible. Validate on focus loss and save, focus the first invalid field, and show each error beneath its field. Keep one primary “保存” button at bottom-right; “测试连接” is secondary and shows loading after 300 ms, then explicit success or recovery-oriented error text.

`HotkeyRecorder` captures a modifier-plus-key combination while focused, formats `Ctrl + Alt + D`, blocks modifier-only input, and provides AutomationProperties names.

- [ ] **Step 5: Implement and test the view model**

`SettingsViewModel` edits a copy of loaded settings. Save order is: validate fields, try registering the new hotkey, update startup registration, atomically save settings, apply theme, then replace the in-memory active settings. On any failure before persistence, retain the previous active configuration and expose a field or page error.

Run:

```powershell
dotnet test tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj --filter SettingsViewModelTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenshotTranslation.App/Themes src/ScreenshotTranslation.App/Settings src/ScreenshotTranslation.App/Services/ThemeService.cs tests/ScreenshotTranslation.App.Tests/Settings
git commit -m "feat: add accessible settings experience"
```

## Task 9: Build the frozen-screen selection overlay

**Files:**
- Create: files under `src/ScreenshotTranslation.App/Overlay/`
- Create: `tests/ScreenshotTranslation.App.Tests/Overlay/OverlayViewModelTests.cs`

- [ ] **Step 1: Write failing overlay-state tests**

Test the user-visible state transitions:

```csharp
[Fact]
public async Task Releasing_a_valid_selection_starts_one_translation()
{
    var fixture = OverlayViewModelFixture.Create();

    fixture.ViewModel.BeginSelection(new PixelPoint(100, 100));
    fixture.ViewModel.UpdatePointer(new PixelPoint(400, 250));
    await fixture.ViewModel.CompletePointerActionAsync();

    Assert.Equal(new PixelRect(100, 100, 300, 150), fixture.ViewModel.Selection);
    Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
}

[Fact]
public async Task Drag_updates_do_not_translate_until_mouse_release()
{
    var fixture = OverlayViewModelFixture.Create(existingSelection: new PixelRect(100, 100, 300, 150));

    fixture.ViewModel.BeginResize(ResizeHandle.Right, new PixelPoint(400, 175));
    fixture.ViewModel.UpdatePointer(new PixelPoint(450, 175));
    fixture.ViewModel.UpdatePointer(new PixelPoint(500, 175));
    Assert.Equal(0, fixture.TranslationCoordinator.ScreenshotCallCount);

    await fixture.ViewModel.CompletePointerActionAsync();
    Assert.Equal(1, fixture.TranslationCoordinator.ScreenshotCallCount);
}
```

Also cover small-selection cancellation, moving, eight-handle resizing, stale result suppression, `Esc`, outside click, no-text, retry, reply target defaulting to detected source language, and copy-close behavior.

Define `OverlayViewModelFixture` in the test file with an in-memory PNG crop fake, controllable translation coordinator fake, clipboard fake, fixed screen bounds, and close callback counter. Add a test proving that changing the screenshot target language after a valid selection causes exactly one new translation of the current crop.

- [ ] **Step 2: Verify overlay tests fail**

Run:

```powershell
dotnet test tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj --filter OverlayViewModelTests
```

Expected: FAIL because overlay types do not exist.

- [ ] **Step 3: Implement coordinate mapping and selection canvas**

`OverlayCoordinateMapper` converts WPF device-independent points to physical screenshot pixels using the overlay HWND DPI. All domain selection rectangles remain physical pixels.

`SelectionCanvas` renders the immutable captured screenshot, a 55% black outside mask, a 2-pixel purple selection border, and eight white circular handles with purple borders. Visual handle diameter is 10 DIPs with at least 18 DIPs of hit area. Draw a subtle non-animated highlight ring around the crosshair pointer. The canvas implements four pointer modes: idle, creating, moving, resizing. Pointer movement only updates geometry; it emits `PointerActionCompleted` once on mouse release.

The window is borderless, topmost, excluded from the taskbar, and physically positioned with `SetWindowPos` using monitor bounds. It listens for `Esc`; a click that is outside both selection and result panel closes the session. A click without a valid selection or a right-click also closes.

- [ ] **Step 4: Implement result-panel layout and state view**

`TranslationPanelView` is anchored below the selection with a 12-pixel gap, flips above when required, and is clamped within the current monitor. Use a 420-pixel minimum width and 720-pixel maximum width. Its stable layout contains status row, screenshot result, and reply section so loading does not shift controls.

Show progress if the request lasts more than 300 ms. Error states contain cause plus retry action. Color is never the only state signal. Bind `Enter` in the single-line reply box to reply translation, while IME composition is active no command is sent. Disable translate/copy buttons while their required content is unavailable.

- [ ] **Step 5: Implement overlay view-model orchestration**

On selection completion, crop from the original frame and call `TranslationCoordinator`. Changing the screenshot target language after a valid selection submits the current crop exactly once. Record the most recent successful source language code. Reply translation defaults to that code and remains manually selectable; if the fallback response has no source code, require manual reply-target selection. Copy writes exactly the visible translation, reports clipboard failure without closing, and on success signals the window to close and restore the previous foreground window.

Run:

```powershell
dotnet test tests/ScreenshotTranslation.App.Tests/ScreenshotTranslation.App.Tests.csproj --filter OverlayViewModelTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenshotTranslation.App/Overlay tests/ScreenshotTranslation.App.Tests/Overlay
git commit -m "feat: add screenshot selection overlay"
```

## Task 10: Compose the tray application and end-to-end workflow

**Files:**
- Create: `src/ScreenshotTranslation.App/Composition/AppServices.cs`
- Create: `src/ScreenshotTranslation.App/Services/TrayIconService.cs`
- Modify: `src/ScreenshotTranslation.App/App.xaml`
- Modify: `src/ScreenshotTranslation.App/App.xaml.cs`
- Create: `tools/Generate-AppIcon.ps1`
- Generate: `src/ScreenshotTranslation.App/Assets/AppIcon.ico`

- [ ] **Step 1: Create a deterministic application icon**

Add `tools/Generate-AppIcon.ps1`:

```powershell
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeIcon {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

$output = Join-Path $PSScriptRoot '..\src\ScreenshotTranslation.App\Assets\AppIcon.ico'
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
function New-RoundedRectanglePath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$bitmap = [System.Drawing.Bitmap]::new(64, 64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$backgroundPath = New-RoundedRectanglePath 1 1 62 62 12
$backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(124, 58, 237))
$graphics.FillPath($backgroundBrush, $backgroundPath)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 4)
$graphics.DrawLine($pen, 12, 24, 12, 12)
$graphics.DrawLine($pen, 12, 12, 24, 12)
$graphics.DrawLine($pen, 40, 12, 52, 12)
$graphics.DrawLine($pen, 52, 12, 52, 24)
$graphics.DrawLine($pen, 12, 40, 12, 52)
$graphics.DrawLine($pen, 12, 52, 24, 52)
$graphics.DrawLine($pen, 40, 52, 52, 52)
$graphics.DrawLine($pen, 52, 40, 52, 52)
$bubblePath = New-RoundedRectanglePath 20 23 24 17 5
$graphics.FillPath([System.Drawing.Brushes]::White, $bubblePath)
$graphics.FillPolygon([System.Drawing.Brushes]::White, @(
    [System.Drawing.Point]::new(25, 39),
    [System.Drawing.Point]::new(25, 46),
    [System.Drawing.Point]::new(32, 39)))
$handle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($handle)
$stream = [System.IO.File]::Create($output)
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
[NativeIcon]::DestroyIcon($handle) | Out-Null
$bubblePath.Dispose()
$backgroundBrush.Dispose()
$backgroundPath.Dispose()
$pen.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
```

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Generate-AppIcon.ps1
```

Expected: a 64-pixel `src/ScreenshotTranslation.App/Assets/AppIcon.ico` exists and is used by the executable, settings window, tray, and installer. The script is the reproducible source for the binary icon.

- [ ] **Step 2: Compose application services manually**

`AppServices` creates one settings store, diagnostic log, `HttpClient`, model client, coordinator, monitor/capture/crop adapters, hotkey service, foreground service, startup service, theme service, and window factories. Do not introduce a service locator inside view models; pass dependencies through constructors.

- [ ] **Step 3: Implement startup flow**

`App.OnStartup` must:

1. Acquire `SingleInstanceCoordinator`.
2. If secondary, send `SHOW_SETTINGS` and shut down.
3. Load settings and apply the theme.
4. Create the tray icon.
5. Register the configured hotkey.
6. Open settings when API Key, URL, or model is missing; otherwise remain tray-only.
7. Start the named-pipe activation listener.

Hotkey or tray “开始截图翻译” captures the foreground HWND, gets the cursor monitor, captures exactly one immutable frame, then opens one overlay. Ignore repeated hotkey presses while an overlay is already active.

- [ ] **Step 4: Implement tray behavior and shutdown**

Left-click opens or activates the single settings window. Right-click menu contains only 开始截图翻译, 设置, and 退出. Closing the settings window hides it without exiting. Explicit 退出 cancels translation, closes overlay/settings, unregisters hotkey, disposes the tray icon and `HttpClient`, stops the pipe listener, releases the mutex, and calls `Application.Shutdown()`.

- [ ] **Step 5: Run the first cross-module verification once**

Run:

```powershell
dotnet build ScreenshotTranslation.sln -c Debug
dotnet test ScreenshotTranslation.sln -c Debug --no-build
```

Expected: build and all automated tests pass. This is the first full-suite run; do not repeat it unless this task exposes a cross-module defect.

- [ ] **Step 6: Perform one focused interactive smoke test**

Run the app and verify only the composed workflow:

1. First launch opens settings.
2. API Key is visible in a normal text box and persists after restart.
3. Tray left/right click behavior matches the specification.
4. `Ctrl + Alt + D` freezes the cursor monitor.
5. A local fake HTTP handler or test endpoint returns deterministic translation JSON.
6. Resize triggers one new request only on release.
7. Copy closes the overlay and restores the prior window.

- [ ] **Step 7: Commit**

```powershell
git add src/ScreenshotTranslation.App tools
git commit -m "feat: compose tray translation workflow"
```

## Task 11: Add installer, CI, and open-source documentation

**Files:**
- Create: `installer/ScreenshotTranslation.Installer.wixproj`
- Create: `installer/Package.wxs`
- Create: `.github/workflows/windows.yml`
- Create: `README.md`
- Modify: `.gitignore`

- [ ] **Step 1: Add a WiX MSI project**

Create `ScreenshotTranslation.Installer.wixproj`:

```xml
<Project Sdk="WixToolset.Sdk/5.0.2">
  <PropertyGroup>
    <OutputType>Package</OutputType>
    <Platform>x64</Platform>
    <PublishDir>$(MSBuildThisFileDirectory)..\artifacts\publish\</PublishDir>
  </PropertyGroup>
</Project>
```

`Package.wxs` must define a per-machine x64 package named `Screenshot Translation`, install all files from `artifacts/publish` under `ProgramFiles64Folder\ScreenshotTranslation`, create a Start Menu shortcut, use `AppIcon.ico`, and register normal MSI uninstall metadata. Do not add automatic updates or launch-on-install.

Use this WiX structure with a fixed upgrade code:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="Screenshot Translation"
           Manufacturer="Screenshot Translation contributors"
           Version="1.0.0"
           UpgradeCode="B6D01339-D36B-4C22-B23A-07AFA70A53F4"
           Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of Screenshot Translation is already installed." />
    <MediaTemplate EmbedCab="yes" />
    <Icon Id="AppIcon" SourceFile="..\src\ScreenshotTranslation.App\Assets\AppIcon.ico" />
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="ScreenshotTranslation" />
    </StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="Screenshot Translation">
        <Component Id="StartMenuShortcut" Guid="*">
          <Shortcut Id="StartMenuShortcutLink"
                    Name="Screenshot Translation"
                    Target="[INSTALLFOLDER]ScreenshotTranslation.exe"
                    WorkingDirectory="INSTALLFOLDER"
                    Icon="AppIcon" />
          <RemoveFolder Id="RemoveApplicationProgramsFolder" On="uninstall" />
          <RegistryValue Root="HKCU"
                         Key="Software\ScreenshotTranslation"
                         Name="Installed"
                         Type="integer"
                         Value="1"
                         KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>
    <Feature Id="MainFeature">
      <ComponentGroupRef Id="PublishedFiles" />
      <ComponentRef Id="StartMenuShortcut" />
    </Feature>
  </Package>
  <Fragment>
    <ComponentGroup Id="PublishedFiles" Directory="INSTALLFOLDER">
      <Files Include="$(PublishDir)**" />
    </ComponentGroup>
  </Fragment>
</Wix>
```

- [ ] **Step 2: Add a Windows CI workflow**

Create `.github/workflows/windows.yml` with these jobs on `windows-latest`:

```yaml
name: windows

on:
  push:
  pull_request:

jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet restore ScreenshotTranslation.sln
      - run: dotnet build ScreenshotTranslation.sln -c Release --no-restore
      - run: dotnet test ScreenshotTranslation.sln -c Release --no-build
      - run: dotnet publish src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
      - run: dotnet build installer/ScreenshotTranslation.Installer.wixproj -c Release
      - uses: actions/upload-artifact@v4
        with:
          name: ScreenshotTranslation-win-x64
          path: |
            artifacts/publish/**
            installer/bin/Release/**/*.msi
```

Do not provide API secrets to CI and do not run the real百炼 integration test by default.

- [ ] **Step 3: Write the open-source README**

Document supported Windows modes, installation, tray behavior, default `Ctrl + Alt + D`, selection/resize/reply flow, all model fields, exact default `qwen3.7-flash`, `enable_thinking: false`, plaintext `%APPDATA%\ScreenshotTranslator\settings.json` API-key storage, no telemetry/history, build/test commands, MSI command, contribution guidance, and the unsigned SmartScreen warning. Link the approved design and implementation plan.

- [ ] **Step 4: Build publish and MSI once**

Run:

```powershell
dotnet publish src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
dotnet build installer/ScreenshotTranslation.Installer.wixproj -c Release
```

Expected: a self-contained executable and one x64 MSI are produced.

- [ ] **Step 5: Commit**

```powershell
git add installer .github README.md .gitignore
git commit -m "build: add Windows installer and CI"
```

## Task 12: Perform risk-proportionate final verification

**Files:**
- Modify only files required by defects found in this task
- Record manual results in the release notes or pull request, not in source code

- [ ] **Step 1: Run one clean automated verification**

Run:

```powershell
dotnet clean ScreenshotTranslation.sln -c Release
dotnet restore ScreenshotTranslation.sln
dotnet build ScreenshotTranslation.sln -c Release --no-restore
dotnet test ScreenshotTranslation.sln -c Release --no-build
git diff --check
```

Expected: clean build, all tests pass, and no whitespace errors. Do not rerun this sequence after documentation-only edits.

- [ ] **Step 2: Scan only for repository secret leakage**

Run:

```powershell
rg -n --hidden -g '!.git/**' -g '!artifacts/**' '(sk-[A-Za-z0-9_-]{12,}|api[_-]?key\s*[:=]\s*[^" ]+)' .
```

Expected: only test fixture values such as `sk-test` and documentation examples; no real key or local `settings.json`.

- [ ] **Step 3: Run the Windows acceptance matrix once**

Verify on Windows 10/11 x64 as available:

- 100%, 125%, 150%, and 200% scaling.
- Primary and secondary display, with only the cursor display frozen.
- Normal and borderless-window game/application.
- Default and changed hotkeys, including a conflict.
- Eight resize handles, selection movement, outside click, right-click, and `Esc`.
- Latest-request-wins after rapid resize.
- Simplified Chinese default target and reply-to-source-language default.
- `401/403`, `429`, timeout, no text, invalid JSON, and clipboard failure.
- Duplicate launch activates the current instance.
- MSI install, first launch, restart persistence, uninstall, and no remaining running process.

If a defect is fixed, rerun only its directly affected automated test and manual scenario. Repeat the full matrix only when the fix changes a shared subsystem such as coordinate mapping, translation coordination, or application lifetime.

- [ ] **Step 4: Review UI against the approved UI/UX constraints**

Check light and dark settings themes independently, screenshot overlay contrast, visible focus, logical Tab order, inline error text, disabled/loading states, stable panel geometry, vector-only functional icons, no decorative motion, and no color-only state. Use Windows high-contrast mode for one smoke pass. Fix only observed violations and rerun their affected window or control checks.

- [ ] **Step 5: Commit verified fixes or mark the implementation complete**

If defects required changes:

```powershell
git add -u
git commit -m "fix: resolve release verification findings"
```

If no defects were found, do not create an empty commit. Record the successful build, test, MSI, and manual matrix results in the implementation handoff.
