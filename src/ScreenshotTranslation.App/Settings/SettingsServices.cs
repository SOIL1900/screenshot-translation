using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.App.Settings;

public interface IHotkeyRegistrationService
{
    HotkeyRegistrationResult TryRegister(HotkeyGesture gesture);
}

public sealed class HotkeyRegistrationService(GlobalHotkeyService service) : IHotkeyRegistrationService
{
    public HotkeyRegistrationResult TryRegister(HotkeyGesture gesture) => service.TryRegister(gesture);
}

public interface IStartupRegistrationService
{
    void SetEnabled(bool enabled);
}

public sealed class StartupRegistrationAdapter(StartupRegistrationService service) : IStartupRegistrationService
{
    public void SetEnabled(bool enabled) => service.SetEnabled(enabled);
}

public interface ISettingsDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemSettingsDelay : ISettingsDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
