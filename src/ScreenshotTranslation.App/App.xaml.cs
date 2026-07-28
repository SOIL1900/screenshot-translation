using System.ComponentModel;
using System.Windows;
using ScreenshotTranslation.App.Composition;
using ScreenshotTranslation.App.Overlay;
using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.App.Settings;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Infrastructure.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace ScreenshotTranslation.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private AppServices? _services;
    private TrayIconService? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private CaptureOverlayWindow? _overlayWindow;
    private AppSettings? _initialSettings;
    private bool _captureStarting;
    private bool _isExiting;
    private bool _resourcesDisposed;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            _singleInstance = new SingleInstanceCoordinator(Dispatcher);
            if (!_singleInstance.IsPrimaryInstance)
            {
                _ = await _singleInstance.NotifyPrimaryAsync();
                _singleInstance.Dispose();
                _singleInstance = null;
                Shutdown();
                return;
            }

            _services = new AppServices(Resources);
            _initialSettings = await _services.SettingsStore.LoadAsync(CancellationToken.None);
            _services.ThemeService.Apply(_initialSettings.General.Theme);

            _trayIcon = _services.CreateTrayIconService();
            _trayIcon.CaptureRequested += OnCaptureRequested;
            _trayIcon.SettingsRequested += OnSettingsRequested;
            _trayIcon.ExitRequested += OnExitRequested;
            _services.HotkeyService.Pressed += OnCaptureRequested;

            var hotkeyResult = _services.HotkeyService.TryRegister(
                _initialSettings.General.CaptureHotkey);
            var mustShowSettings = !IsModelConfigured(_initialSettings) || !hotkeyResult.Succeeded;
            if (!hotkeyResult.Succeeded)
            {
                await LogAsync("hotkey_registration_failed");
                _trayIcon.ShowError(
                    hotkeyResult.ErrorMessage ?? "快捷键注册失败，请在设置中选择其他组合。");
            }

            if (mustShowSettings)
            {
                ShowSettingsWindow();
            }

            _singleInstance.StartListening(ShowSettingsWindow);
        }
        catch (Exception exception)
        {
            await LogAsync("startup_failed", exception);
            _ = WpfMessageBox.Show(
                "应用启动失败，请重启后再试。",
                "截图翻译",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ExitApplication(1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        _isExiting = true;
        DisposeResources();
        base.OnExit(eventArgs);
    }

    private async void OnCaptureRequested(object? sender, EventArgs eventArgs) =>
        await BeginCaptureAsync();

    private void OnSettingsRequested(object? sender, EventArgs eventArgs) =>
        ShowSettingsWindow();

    private void OnExitRequested(object? sender, EventArgs eventArgs) =>
        ExitApplication();

    private void ShowSettingsWindow()
    {
        if (_isExiting || _services is null || _initialSettings is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = _services.CreateSettingsWindow(_initialSettings);
            _settingsWindow.Closing += OnSettingsWindowClosing;
            _settingsWindow.Closed += OnSettingsWindowClosed;
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _ = _settingsWindow.Activate();
    }

    private void OnSettingsWindowClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (_isExiting || sender is not SettingsWindow window)
        {
            return;
        }

        eventArgs.Cancel = true;
        window.Hide();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (sender is SettingsWindow window)
        {
            window.Closing -= OnSettingsWindowClosing;
            window.Closed -= OnSettingsWindowClosed;
        }

        _settingsWindow = null;
    }

    private async Task BeginCaptureAsync()
    {
        var services = _services;
        if (_isExiting || _captureStarting || _overlayWindow is not null || services is null)
        {
            return;
        }

        _captureStarting = true;
        nint previousForegroundWindow = nint.Zero;
        var captureStage = "load_settings";
        try
        {
            var settings = await services.SettingsStore.LoadAsync(CancellationToken.None);
            captureStage = "validate_settings";
            if (_isExiting)
            {
                return;
            }

            if (!IsModelConfigured(settings))
            {
                _trayIcon?.ShowError("请先在设置中完成 API Key、服务地址和模型配置。");
                ShowSettingsWindow();
                return;
            }

            captureStage = "capture_foreground_window";
            previousForegroundWindow = services.ForegroundWindowService.CaptureForegroundWindow();
            captureStage = "locate_monitor";
            var monitor = services.MonitorService.GetMonitorUnderCursor();
            captureStage = "capture_screen";
            var frame = services.ScreenCaptureService.Capture(monitor);
            captureStage = "create_overlay";
            var overlay = services.CreateOverlayWindow(
                frame,
                settings,
                previousForegroundWindow);
            _overlayWindow = overlay;
            overlay.Closed += OnOverlayClosed;
            captureStage = "show_overlay";
            overlay.Show();
        }
        catch (Exception exception)
        {
            if (!exception.Data.Contains("CaptureStage"))
            {
                exception.Data["CaptureStage"] = captureStage;
            }
            if (_overlayWindow is { } overlay)
            {
                overlay.Closed -= OnOverlayClosed;
                _overlayWindow = null;
                overlay.Close();
            }

            if (previousForegroundWindow != nint.Zero)
            {
                _ = services.ForegroundWindowService.RestoreForegroundWindow(
                    previousForegroundWindow);
            }

            await LogAsync("capture_workflow_failed", exception);
            _trayIcon?.ShowError("截图翻译启动失败，请重试。");
        }
        finally
        {
            _captureStarting = false;
        }
    }

    private void OnOverlayClosed(object? sender, EventArgs eventArgs)
    {
        if (sender is CaptureOverlayWindow overlay)
        {
            overlay.Closed -= OnOverlayClosed;
            if (ReferenceEquals(_overlayWindow, overlay))
            {
                _overlayWindow = null;
            }
        }
    }

    private void ExitApplication(int exitCode = 0)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        DisposeResources();
        Shutdown(exitCode);
    }

    private void DisposeResources()
    {
        if (_resourcesDisposed)
        {
            return;
        }

        _resourcesDisposed = true;
        _services?.TranslationCoordinator.Cancel();

        if (_overlayWindow is { } overlay)
        {
            overlay.Closed -= OnOverlayClosed;
            _overlayWindow = null;
            overlay.Close();
        }

        if (_settingsWindow is { } settingsWindow)
        {
            settingsWindow.Closing -= OnSettingsWindowClosing;
            settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow = null;
            settingsWindow.Close();
        }

        if (_services is { } services)
        {
            services.HotkeyService.Pressed -= OnCaptureRequested;
            services.HotkeyService.Dispose();
        }

        if (_trayIcon is { } trayIcon)
        {
            trayIcon.CaptureRequested -= OnCaptureRequested;
            trayIcon.SettingsRequested -= OnSettingsRequested;
            trayIcon.ExitRequested -= OnExitRequested;
            trayIcon.Dispose();
            _trayIcon = null;
        }

        _services?.Dispose();
        _services = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    private async Task LogAsync(string eventName, Exception? exception = null)
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            await _services.DiagnosticLog.WriteAsync(eventName, exception);
        }
        catch (Exception)
        {
        }
    }

    private static bool IsModelConfigured(AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.Model.ApiKey) &&
        !string.IsNullOrWhiteSpace(settings.Model.BaseUrl) &&
        !string.IsNullOrWhiteSpace(settings.Model.ModelName);
}
