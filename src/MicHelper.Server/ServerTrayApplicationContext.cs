using System.Drawing;
using System.Windows.Forms;
using MicHelper.Server.Config;
using MicHelper.Server.UI;
using MicHelper.Shared.Audio;
using MicHelper.Shared.Network;
using MicHelper.Shared.Protocol;
using MicHelper.Shared.UI;

namespace MicHelper.Server;

public sealed class ServerTrayApplicationContext : ApplicationContext
{
    private readonly ServerSettings _settings;
    private readonly IAudioMonitor _audioMonitor;
    private readonly UdpBroadcaster _broadcaster;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _menuPauseResume;
    private readonly ToolStripMenuItem _menuSettings;
    private readonly ToolStripMenuItem _menuExit;
    private readonly ContextMenuStrip _contextMenu;

    private readonly SynchronizationContext _syncContext;
    private ServerSettingsForm? _settingsForm;
    private MicState _lastState = MicState.Unmuted;

    public ServerTrayApplicationContext()
    {
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = ServerSettings.Load();

        _broadcaster = new UdpBroadcaster(_settings.Port);
        _audioMonitor = new WindowsAudioMonitor();

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
            Text = "Mic Helper Server",
            Icon = StatusIconGenerator.GetAppIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        // Subscribe to events
        _audioMonitor.MuteChanged += OnAudioMuteChanged;
        _audioMonitor.ConnectionChanged += OnAudioConnectionChanged;
        _audioMonitor.DevicesChanged += OnDevicesChanged;

        // Apply startup settings
        if (_settings.IsPaused)
        {
            _menuPauseResume.Text = "Resume";
            _broadcaster.SetPaused(true);
            UpdateStatus(MicState.Paused);
        }
        else
        {
            _audioMonitor.StartMonitoring(_settings.MicrophoneId, _settings.RetryTimeout);
            _broadcaster.Start();
            UpdateAudioState();
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

    private void UpdateAudioState()
    {
        if (_settings.IsPaused)
        {
            UpdateStatus(MicState.Paused);
            return;
        }

        if (!_audioMonitor.IsConnected)
        {
            UpdateStatus(MicState.Disconnected);
            _broadcaster.UpdateState(MicState.Disconnected, _settings.MicrophoneName);
            return;
        }

        var micState = _audioMonitor.IsMuted ? MicState.Muted : MicState.Unmuted;
        var micName = _audioMonitor.CurrentDeviceName ?? _settings.MicrophoneName;
        _broadcaster.UpdateState(micState, micName);
        UpdateStatus(micState);
    }

    private void OnAudioMuteChanged(bool isMuted)
    {
        PostToUiThread(UpdateAudioState);
    }

    private void OnAudioConnectionChanged(bool isConnected)
    {
        PostToUiThread(UpdateAudioState);
    }

    private void OnDevicesChanged()
    {
        PostToUiThread(UpdateAudioState);
    }

    private void UpdateStatus(MicState state)
    {
        _lastState = state;
        try
        {
            using var icon = StatusIconGenerator.CreateServerStatusIcon(state);
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = (Icon)icon.Clone();
            oldIcon?.Dispose();

            var statusText = state switch
            {
                MicState.Paused => "Paused",
                MicState.Disconnected => "Device Disconnected",
                MicState.Muted => "Microphone Muted",
                MicState.Unmuted => "Microphone Unmuted",
                _ => "Unknown"
            };

            var micName = !string.IsNullOrEmpty(_settings.MicrophoneName) ? $" - {_settings.MicrophoneName}" : "";
            _trayIcon.Text = TruncateText($"Mic Helper Server ({statusText}){micName}", 63);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update tray icon: {ex.Message}");
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
        _broadcaster.SetPaused(newPaused);

        if (newPaused)
        {
            _audioMonitor.StopMonitoring();
            UpdateStatus(MicState.Paused);
        }
        else
        {
            _audioMonitor.StartMonitoring(_settings.MicrophoneId, _settings.RetryTimeout);
            UpdateAudioState();
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
            _settingsForm = new ServerSettingsForm(
                _settings,
                _audioMonitor,
                onPortChanged: newPort => _broadcaster.Rebind(newPort),
                onMicrophoneChanged: newMicId =>
                {
                    if (!_settings.IsPaused)
                    {
                        _audioMonitor.StartMonitoring(newMicId, _settings.RetryTimeout);
                        UpdateAudioState();
                    }
                },
                onTimeoutChanged: newTimeout =>
                {
                    if (!_settings.IsPaused)
                    {
                        _audioMonitor.StartMonitoring(_settings.MicrophoneId, newTimeout);
                    }
                });

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

        _audioMonitor.Dispose();
        _broadcaster.Dispose();

        base.ExitThreadCore();
    }
}
