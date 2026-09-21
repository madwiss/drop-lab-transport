using System.Drawing;
using Forms = System.Windows.Forms;

namespace Drop.Windows;

public sealed class DropTrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Action _showWindow;
    private readonly Action _exitApplication;

    public DropTrayIcon(
        Action showWindow,
        Action exitApplication)
    {
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentNullException.ThrowIfNull(exitApplication);

        _showWindow = showWindow;
        _exitApplication = exitApplication;

        Forms.ContextMenuStrip menu = new();

        Forms.ToolStripMenuItem openItem =
            new("Open Drop");
        openItem.Click += (_, _) => _showWindow();

        Forms.ToolStripMenuItem exitItem =
            new("Exit Drop");
        exitItem.Click += (_, _) => _exitApplication();

        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Drop",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => _showWindow();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}