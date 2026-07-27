using System.Drawing;
using System.Windows.Forms;

namespace ScreenshotTranslation.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Icon _icon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _exitItem;
    private bool _disposed;

    public TrayIconService(string iconPath)
        : this(iconPath, visible: true)
    {
    }

    internal TrayIconService(string iconPath, bool visible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        _icon = new Icon(iconPath);
        _captureItem = new ToolStripMenuItem("开始截图翻译");
        _settingsItem = new ToolStripMenuItem("设置");
        _exitItem = new ToolStripMenuItem("退出");
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.AddRange([_captureItem, _settingsItem, _exitItem]);
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _icon,
            Text = "截图翻译",
            Visible = visible
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _captureItem.Click += OnCaptureItemClick;
        _settingsItem.Click += OnSettingsItemClick;
        _exitItem.Click += OnExitItemClick;
    }

    public event EventHandler? CaptureRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    internal IReadOnlyList<string> MenuItemTexts =>
        _contextMenu.Items.Cast<ToolStripItem>().Select(item => item.Text ?? string.Empty).ToArray();

    public void ShowError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _notifyIcon.ShowBalloonTip(
            timeout: 4_000,
            tipTitle: "截图翻译",
            tipText: message,
            tipIcon: ToolTipIcon.Error);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.MouseClick -= OnNotifyIconMouseClick;
        _captureItem.Click -= OnCaptureItemClick;
        _settingsItem.Click -= OnSettingsItemClick;
        _exitItem.Click -= OnExitItemClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _icon.Dispose();
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCaptureItemClick(object? sender, EventArgs eventArgs) =>
        CaptureRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsItemClick(object? sender, EventArgs eventArgs) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitItemClick(object? sender, EventArgs eventArgs) =>
        ExitRequested?.Invoke(this, EventArgs.Empty);
}
