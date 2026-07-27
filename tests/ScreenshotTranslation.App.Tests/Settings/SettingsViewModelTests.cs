using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.App.Settings;
using ScreenshotTranslation.Core.Abstractions;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.App.Tests.Settings;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Save_keeps_plaintext_api_key_and_applies_the_new_hotkey()
    {
        var fixture = SettingsViewModelFixture.Create();
        fixture.ViewModel.ApiKey = "sk-visible-value";
        fixture.ViewModel.CaptureHotkey = new HotkeyGesture(
            HotkeyModifiers.Control | HotkeyModifiers.Alt,
            0x44);

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
        Assert.Null(fixture.StartupService.Applied);
        Assert.Null(fixture.ThemeService.Applied);
    }

    [Fact]
    public async Task Save_recovers_when_hotkey_registration_throws()
    {
        var fixture = SettingsViewModelFixture.Create(
            hotkeyFailure: new InvalidOperationException("Native registration failed"));
        fixture.ViewModel.ApiKey = "sk-edited";

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Contains("重试", fixture.ViewModel.Errors["General.CaptureHotkey"]);
        Assert.Contains("原设置仍然有效", fixture.ViewModel.PageError);
        Assert.Null(fixture.SettingsStore.Saved);
        Assert.Null(fixture.StartupService.Applied);
        Assert.Null(fixture.ThemeService.Applied);
        Assert.True(fixture.ViewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Model_field_validation_reports_recovery_next_to_the_field()
    {
        var fixture = SettingsViewModelFixture.Create();
        fixture.ViewModel.BaseUrl = "not-a-url";

        var isValid = fixture.ViewModel.ValidateField("Model.BaseUrl");

        Assert.False(isValid);
        Assert.Contains("HTTP", fixture.ViewModel.Errors["Model.BaseUrl"]);
        Assert.Equal("Model.BaseUrl", fixture.ViewModel.FirstInvalidField);
    }

    [Fact]
    public void Save_is_disabled_while_settings_are_unchanged()
    {
        var fixture = SettingsViewModelFixture.Create();

        Assert.False(fixture.ViewModel.SaveCommand.CanExecute(null));

        fixture.ViewModel.ModelName = "another-model";

        Assert.True(fixture.ViewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Connection_test_shows_delayed_loading_then_success()
    {
        var fixture = SettingsViewModelFixture.Create(
            holdConnectionTest: true,
            holdLoadingDelay: true);

        var execution = fixture.ViewModel.TestConnectionCommand.ExecuteAsync(null);
        Assert.False(fixture.ViewModel.IsConnectionTestLoading);

        fixture.Delay.Complete();
        await WaitUntilAsync(() => fixture.ViewModel.IsConnectionTestLoading);
        Assert.Equal(ConnectionTestState.Testing, fixture.ViewModel.ConnectionTestState);

        fixture.TranslationClient.CompleteConnectionTest();
        await execution;

        Assert.False(fixture.ViewModel.IsConnectionTestLoading);
        Assert.Equal(ConnectionTestState.Success, fixture.ViewModel.ConnectionTestState);
        Assert.Contains("连接成功", fixture.ViewModel.ConnectionTestMessage);
    }

    [Fact]
    public async Task Connection_test_exposes_recovery_oriented_error()
    {
        var fixture = SettingsViewModelFixture.Create(
            connectionFailure: new TranslationClientException(
                TranslationErrorCode.Unauthorized,
                "Unauthorized"));

        await fixture.ViewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(ConnectionTestState.Error, fixture.ViewModel.ConnectionTestState);
        Assert.Contains("API Key", fixture.ViewModel.ConnectionTestMessage);
        Assert.Contains("重试", fixture.ViewModel.ConnectionTestMessage);
    }

    [Fact]
    public async Task Save_applies_selected_theme_after_persistence()
    {
        var fixture = SettingsViewModelFixture.Create();
        fixture.ViewModel.Theme = AppTheme.Dark;

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppTheme.Dark, fixture.SettingsStore.Saved!.General.Theme);
        Assert.Equal(AppTheme.Dark, fixture.ThemeService.Applied);
        Assert.False(fixture.ViewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_updates_current_user_startup_registration()
    {
        var fixture = SettingsViewModelFixture.Create();
        fixture.ViewModel.RunAtStartup = true;

        await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(fixture.StartupService.Applied);
        Assert.True(fixture.SettingsStore.Saved!.General.RunAtStartup);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    private sealed class SettingsViewModelFixture
    {
        private SettingsViewModelFixture(
            SettingsViewModel viewModel,
            FakeSettingsStore settingsStore,
            FakeHotkeyRegistrationService hotkeyService,
            FakeStartupRegistrationService startupService,
            FakeThemeService themeService,
            FakeTranslationClient translationClient,
            FakeSettingsDelay delay)
        {
            ViewModel = viewModel;
            SettingsStore = settingsStore;
            HotkeyService = hotkeyService;
            StartupService = startupService;
            ThemeService = themeService;
            TranslationClient = translationClient;
            Delay = delay;
        }

        public SettingsViewModel ViewModel { get; }

        public FakeSettingsStore SettingsStore { get; }

        public FakeHotkeyRegistrationService HotkeyService { get; }

        public FakeStartupRegistrationService StartupService { get; }

        public FakeThemeService ThemeService { get; }

        public FakeTranslationClient TranslationClient { get; }

        public FakeSettingsDelay Delay { get; }

        public static SettingsViewModelFixture Create(
            bool hotkeyRegistrationSucceeds = true,
            bool holdConnectionTest = false,
            bool holdLoadingDelay = false,
            Exception? connectionFailure = null,
            Exception? hotkeyFailure = null)
        {
            var activeSettings = AppSettings.CreateDefault() with
            {
                Model = AppSettings.CreateDefault().Model with { ApiKey = "sk-original" }
            };
            var settingsStore = new FakeSettingsStore(activeSettings);
            var hotkeyService = new FakeHotkeyRegistrationService(
                hotkeyRegistrationSucceeds,
                hotkeyFailure);
            var startupService = new FakeStartupRegistrationService();
            var themeService = new FakeThemeService();
            var translationClient = new FakeTranslationClient(holdConnectionTest, connectionFailure);
            var delay = new FakeSettingsDelay(holdLoadingDelay);
            var viewModel = new SettingsViewModel(
                activeSettings,
                settingsStore,
                hotkeyService,
                startupService,
                themeService,
                translationClient,
                delay);
            return new SettingsViewModelFixture(
                viewModel,
                settingsStore,
                hotkeyService,
                startupService,
                themeService,
                translationClient,
                delay);
        }
    }

    private sealed class FakeSettingsStore(AppSettings loaded) : ISettingsStore
    {
        public AppSettings? Saved { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(loaded);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHotkeyRegistrationService(
        bool succeeds,
        Exception? failure) : IHotkeyRegistrationService
    {
        public HotkeyGesture? Registered { get; private set; }

        public HotkeyRegistrationResult TryRegister(HotkeyGesture gesture)
        {
            if (failure is not null)
            {
                throw failure;
            }

            Registered = gesture;
            return succeeds
                ? HotkeyRegistrationResult.Success
                : new HotkeyRegistrationResult(false, "conflict");
        }
    }

    private sealed class FakeStartupRegistrationService : IStartupRegistrationService
    {
        public bool? Applied { get; private set; }

        public void SetEnabled(bool enabled) => Applied = enabled;
    }

    private sealed class FakeThemeService : IThemeService
    {
        public AppTheme? Applied { get; private set; }

        public void Apply(AppTheme theme) => Applied = theme;
    }

    private sealed class FakeTranslationClient : ITranslationClient
    {
        private readonly TaskCompletionSource _connectionCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _holdConnectionTest;
        private readonly Exception? _connectionFailure;

        public FakeTranslationClient(bool holdConnectionTest, Exception? connectionFailure)
        {
            _holdConnectionTest = holdConnectionTest;
            _connectionFailure = connectionFailure;
        }

        public void CompleteConnectionTest() => _connectionCompletion.TrySetResult();

        public Task TestConnectionAsync(ModelSettings settings, CancellationToken cancellationToken)
        {
            if (_connectionFailure is not null)
            {
                return Task.FromException(_connectionFailure);
            }

            return _holdConnectionTest ? _connectionCompletion.Task : Task.CompletedTask;
        }

        public Task<ScreenshotTranslationResult> TranslateScreenshotAsync(
            ReadOnlyMemory<byte> pngBytes,
            string targetLanguageCode,
            ModelSettings settings,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReplyTranslationResult> TranslateReplyAsync(
            string input,
            string targetLanguageCode,
            ModelSettings settings,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSettingsDelay(bool hold) : ISettingsDelay
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete() => _completion.TrySetResult();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            hold ? _completion.Task : Task.CompletedTask;
    }
}
