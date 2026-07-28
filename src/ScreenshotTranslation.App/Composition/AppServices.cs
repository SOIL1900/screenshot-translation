using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using ScreenshotTranslation.App.Overlay;
using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.App.Settings;
using ScreenshotTranslation.Core.Abstractions;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Geometry;
using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Configuration;
using ScreenshotTranslation.Infrastructure.Diagnostics;
using ScreenshotTranslation.Infrastructure.Translation;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.App.Composition;

public sealed class AppServices : IDisposable
{
    private const string DataDirectoryName = "ScreenshotTranslator";
    private const string OutputIconFileName = "AppIcon.ico";

    private bool _disposed;

    public AppServices(ResourceDictionary applicationResources)
        : this(
            applicationResources,
            GetDefaultDataDirectory(),
            Environment.ProcessPath ?? typeof(AppServices).Assembly.Location,
            TimeProvider.System,
            handler: null)
    {
    }

    internal AppServices(
        ResourceDictionary applicationResources,
        string baseDirectory,
        string executablePath,
        TimeProvider timeProvider,
        HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(timeProvider);

        SettingsStore = new JsonSettingsStore(baseDirectory, timeProvider);
        DiagnosticLog = new FileDiagnosticLog(baseDirectory, timeProvider);
        HttpClient = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);
        TranslationClient = new OpenAiTranslationClient(HttpClient);
        TranslationCoordinator = new TranslationCoordinator(TranslationClient);
        OverlayTranslationCoordinator = new OverlayTranslationCoordinator(TranslationCoordinator);
        MonitorService = new MonitorService();
        ScreenCaptureService = new GdiScreenCaptureService();
        PngCropService = new PngCropService();
        HotkeyService = new GlobalHotkeyService();
        ForegroundWindowService = new ForegroundWindowService();
        StartupRegistrationService = new StartupRegistrationService(executablePath);
        ThemeService = new ThemeService(applicationResources);
    }

    public ISettingsStore SettingsStore { get; }

    public IDiagnosticLog DiagnosticLog { get; }

    public HttpClient HttpClient { get; }

    public ITranslationClient TranslationClient { get; }

    public TranslationCoordinator TranslationCoordinator { get; }

    public IMonitorService MonitorService { get; }

    public IScreenCaptureService ScreenCaptureService { get; }

    public IPngCropService PngCropService { get; }

    public GlobalHotkeyService HotkeyService { get; }

    public ForegroundWindowService ForegroundWindowService { get; }

    public StartupRegistrationService StartupRegistrationService { get; }

    public IThemeService ThemeService { get; }

    internal IOverlayTranslationCoordinator OverlayTranslationCoordinator { get; }

    public SettingsWindow CreateSettingsWindow(AppSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);

        var viewModel = new SettingsViewModel(
            settings,
            SettingsStore,
            new HotkeyRegistrationService(HotkeyService),
            new StartupRegistrationAdapter(StartupRegistrationService),
            ThemeService,
            TranslationClient,
            new SystemSettingsDelay());
        var window = new SettingsWindow(viewModel);
        ApplyWindowIcon(window);
        return window;
    }

    public CaptureOverlayWindow CreateOverlayWindow(
        CapturedMonitorFrame frame,
        AppSettings settings,
        nint previousForegroundWindow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(settings);

        var monitorBounds = frame.Monitor.PhysicalBounds;
        var immutableFrame = new CapturedMonitorFrame(frame.Monitor, frame.PngBytes.ToArray());
        var viewModel = new OverlayViewModel(
            immutableFrame.PngBytes,
            new PixelRect(0, 0, monitorBounds.Width, monitorBounds.Height),
            existingSelection: null,
            settings.General.DefaultTargetLanguage,
            settings.Model,
            PngCropService,
            OverlayTranslationCoordinator,
            new WpfOverlayClipboardService(),
            new SystemOverlayDelay());
        return new CaptureOverlayWindow(
            immutableFrame,
            viewModel,
            previousForegroundWindow,
            ForegroundWindowService);
    }

    public TrayIconService CreateTrayIconService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new TrayIconService(GetOutputIconPath());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TranslationCoordinator.Cancel();
        HotkeyService.Dispose();
        HttpClient.Dispose();
    }

    internal static string GetDefaultDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        DataDirectoryName);

    private static string GetOutputIconPath() =>
        Path.Combine(AppContext.BaseDirectory, OutputIconFileName);

    private static void ApplyWindowIcon(Window window)
    {
        var iconPath = GetOutputIconPath();
        using var stream = File.OpenRead(iconPath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        window.Icon = decoder.Frames[0];
    }
}
