using Microsoft.Win32;

namespace ScreenshotTranslation.Infrastructure.Windows;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenshotTranslation";
    private readonly string _startupCommand;

    public StartupRegistrationService(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _startupCommand = $"\"{Path.GetFullPath(executablePath)}\"";
    }

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(
            runKey?.GetValue(ValueName) as string,
            _startupCommand,
            StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            runKey.SetValue(ValueName, _startupCommand, RegistryValueKind.String);
            return;
        }

        using var existingRunKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        existingRunKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
