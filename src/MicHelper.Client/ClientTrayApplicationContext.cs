using System.Drawing;
using System.Windows.Forms;
using MicHelper.Client.Config;
using MicHelper.Client.Overlay;
using MicHelper.Client.UI;
using MicHelper.Shared.Network;
using MicHelper.Shared.Protocol;
using MicHelper.Shared.UI;

namespace MicHelper.Client;

public sealed class ClientTrayApplicationContext : ApplicationContext
{
    private readonly ClientSettings _settings;
    private readonly OverlayAssetManager _assetManager;
    private readonly OverlayForm _overlayForm;
    private readonly UdpListener _udpListener;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _menuPauseResume;
    private readonly ToolStripMenuItem _menuSettings;
    private readonly ToolStripMenuItem _menuExit;
    private readonly ContextMenuStrip _contextMenu;

    private readonly SynchronizationContext _syncContext;
    private ClientSettingsForm? _settingsForm;

    public ClientTrayApplicationContext()
    {
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = ClientSettings.Load();
        _assetManager = new OverlayAssetManager();
        _overlayForm = new OverlayForm(_settings, _assetManager);
        _udpListener = new UdpListener(_settings.Port);

        // Build Tray Menu
        _contextMenu = new ContextMenuStrip();
        _menuPauseResume = new ToolStripMenuItem("Pause", null, OnPauseResumeClicked);
        _menuSettings = new ToolStripMenuItem("Settings...", null, OnSettingsClicked);
        _menuExit = new ToolStripMenuItem("Exit", null, OnExitClicked);

        _contextMenu.Items.Add(_menuPauseResume);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_menuSettings);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_menuExit);

        _trayIcon = new NotifyIcon
        {
            Text = "Mic Helper Client",
            Icon = StatusIconGenerator.GetAppIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        // Subscribe to listener events
        _udpListener.TargetServerStateChanged += OnServerStateChanged;
        _udpListener.TargetServerConnectionChanged += OnServerConnectionChanged;

        // Apply startup configuration
        if (_settings.IsPaused)
        {
            _menuPauseResume.Text = "Resume";
            _overlayForm.SetLiveState(MicState.Paused, isPaused: true);
            UpdateStatus(MicState.Paused);
        }
        else
        {
            UpdateStatus(MicState.Disconnected);
            _overlayForm.SetLiveState(MicState.Disconnected, isPaused: false);
            _udpListener.Start(_settings.ServerIp, _settings.RetryTimeout);
        }
    }

    private void PostToUiThread(Action action)
    {
        if (SynchronizationContext.Current == _syncContext)
        {
            action();
        }
        else
        {
            _syncContext.Post(_ => action(), null);
        }
    }

    private void OnServerStateChanged(MicState state)
    {
        PostToUiThread(() =>
        {
            if (!_settings.IsPaused)
            {
                _overlayForm.SetLiveState(state, isPaused: false);
                UpdateStatus(state);
            }
        });
    }

    private void OnServerConnectionChanged(bool isConnected)
    {
        PostToUiThread(() =>
        {
            if (!_settings.IsPaused)
            {
                var state = isConnected ? _udpListener.LastReportedState : MicState.Disconnected;
                _overlayForm.SetLiveState(state, isPaused: false);
                UpdateStatus(state);
            }
        });
    }

    private void UpdateStatus(MicState state)
    {
        try
        {
            using var icon = StatusIconGenerator.CreateClientStatusIcon(state);
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = (Icon)icon.Clone();
            oldIcon?.Dispose();

            var statusText = state switch
            {
                MicState.Paused => "Paused",
                MicState.Disconnected => "Server Disconnected",
                MicState.Muted => "Microphone Muted",
                MicState.Unmuted => "Microphone Unmuted",
                _ => "Unknown"
            };

            var srvText = !string.IsNullOrEmpty(_settings.ServerIp) ? $" - {_settings.ServerIp}" : "";
            _trayIcon.Text = TruncateText($"Mic Helper Client ({statusText}){srvText}", 63);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update client tray icon: {ex.Message}");
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }

    private void OnPauseResumeClicked(object? sender, EventArgs e)
    {
        bool newPaused = !_settings.IsPaused;
        _settings.IsPaused = newPaused;
        _settings.Save();

        _menuPauseResume.Text = newPaused ? "Resume" : "Pause";
        _udpListener.SetPaused(newPaused);

        if (newPaused)
        {
            _overlayForm.SetLiveState(MicState.Paused, isPaused: true);
            UpdateStatus(MicState.Paused);
        }
        else
        {
            _overlayForm.SetLiveState(MicState.Disconnected, isPaused: false);
            UpdateStatus(MicState.Disconnected);
            _udpListener.Start(_settings.ServerIp, _settings.RetryTimeout);
        }
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        ShowSettings();
    }

    private void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new ClientSettingsForm(
                _settings,
                _udpListener,
                _overlayForm,
                onPortChanged: newPort => _udpListener.Rebind(newPort, _settings.ServerIp));

            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
            _settingsForm.Show();
        }
        else
        {
            _settingsForm.BringToFront();
            _settingsForm.Activate();
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        _overlayForm.Close();
        _overlayForm.Dispose();
        _assetManager.Dispose();
        _udpListener.Dispose();

        base.ExitThreadCore();
    }
}
