using System.Drawing;
using System.Windows.Forms;
using MicHelper.Client.Config;
using MicHelper.Client.Overlay;
using MicHelper.Client.UI;
using MicHelper.Shared.Audio;
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
    private readonly IAudioMonitor _audioMonitor;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _menuPauseResume;
    private readonly ToolStripMenuItem _menuSettings;
    private readonly ToolStripMenuItem _menuExit;
    private readonly ContextMenuStrip _contextMenu;

    private readonly SynchronizationContext _syncContext;
    private ClientSettingsForm? _settingsForm;
    private ClientMode _activeMode;

    public ClientTrayApplicationContext() : this(null, null, null, null, null)
    {
    }

    internal ClientTrayApplicationContext(
        ClientSettings? settings,
        UdpListener? udpListener,
        IAudioMonitor? audioMonitor,
        OverlayAssetManager? assetManager,
        OverlayForm? overlayForm)
    {
        _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = settings ?? ClientSettings.Load();
        _assetManager = assetManager ?? new OverlayAssetManager();
        _overlayForm = overlayForm ?? new OverlayForm(_settings, _assetManager);
        _udpListener = udpListener ?? new UdpListener(_settings.Port);
        _audioMonitor = audioMonitor ?? new WindowsAudioMonitor();
        _activeMode = _settings.Mode;

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

        // Subscribe to audio monitor events
        _audioMonitor.MuteChanged += OnAudioMuteChanged;
        _audioMonitor.ConnectionChanged += OnAudioConnectionChanged;
        _audioMonitor.DevicesChanged += OnDevicesChanged;

        // Apply startup configuration based on Mode and IsPaused
        if (_settings.IsPaused)
        {
            _menuPauseResume.Text = "Resume";
            _overlayForm.SetLiveState(MicState.Paused, isPaused: true);
            UpdateStatus(MicState.Paused);
        }
        else if (_activeMode == ClientMode.SinglePc)
        {
            _udpListener.Stop();
            _audioMonitor.StartMonitoring(_settings.MicrophoneId, _settings.RetryTimeout);
            UpdateAudioState();
        }
        else // DualPc
        {
            _audioMonitor.StopMonitoring();
            UpdateStatus(MicState.Disconnected);
            _overlayForm.SetLiveState(MicState.Disconnected, isPaused: false);
            _udpListener.Start(_settings.ServerIp, _settings.RetryTimeout);
        }
    }

    internal ClientSettings Settings => _settings;
    internal ClientMode ActiveMode => _activeMode;
    internal UdpListener UdpListener => _udpListener;
    internal IAudioMonitor AudioMonitor => _audioMonitor;
    internal OverlayForm OverlayForm => _overlayForm;
    internal NotifyIcon TrayIcon => _trayIcon;

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

    public void SwitchMode(ClientMode newMode)
    {
        PostToUiThread(() =>
        {
            if (_activeMode == newMode && _settings.Mode == newMode) return;

            _activeMode = newMode;
            _settings.Mode = newMode;
            _settings.Save();

            if (_settings.IsPaused)
            {
                _udpListener.Stop();
                _audioMonitor.StopMonitoring();
                _overlayForm.SetLiveState(MicState.Paused, isPaused: true);
                UpdateStatus(MicState.Paused);
                return;
            }

            if (newMode == ClientMode.SinglePc)
            {
                _udpListener.Stop();
                _audioMonitor.StartMonitoring(_settings.MicrophoneId, _settings.RetryTimeout);
                UpdateAudioState();
            }
            else // DualPc
            {
                _audioMonitor.StopMonitoring();
                _overlayForm.SetLiveState(MicState.Disconnected, isPaused: false);
                UpdateStatus(MicState.Disconnected);
                _udpListener.Start(_settings.ServerIp, _settings.RetryTimeout);
            }
        });
    }

    public void SetMicrophone(string? micId, string? micName)
    {
        PostToUiThread(() =>
        {
            _settings.MicrophoneId = micId;
            _settings.MicrophoneName = micName;
            _settings.Save();

            if (_activeMode == ClientMode.SinglePc && !_settings.IsPaused)
            {
                _audioMonitor.StartMonitoring(micId, _settings.RetryTimeout);
                UpdateAudioState();
            }
        });
    }

    public void SetRetryTimeout(int timeout)
    {
        PostToUiThread(() =>
        {
            _settings.RetryTimeout = timeout;
            _settings.Save();

            if (!_settings.IsPaused)
            {
                if (_activeMode == ClientMode.SinglePc)
                {
                    _audioMonitor.StartMonitoring(_settings.MicrophoneId, timeout);
                }
                else
                {
                    _udpListener.Start(_settings.ServerIp, timeout);
                }
            }
        });
    }

    private void UpdateAudioState()
    {
        if (_activeMode != ClientMode.SinglePc)
        {
            return;
        }

        if (_settings.IsPaused)
        {
            _overlayForm.SetLiveState(MicState.Paused, isPaused: true);
            UpdateStatus(MicState.Paused);
            return;
        }

        if (!_audioMonitor.IsConnected)
        {
            _overlayForm.SetLiveState(MicState.Disconnected, isPaused: false);
            UpdateStatus(MicState.Disconnected);
            return;
        }

        var micState = _audioMonitor.IsMuted ? MicState.Muted : MicState.Unmuted;
        _overlayForm.SetLiveState(micState, isPaused: false);
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

    private void OnServerStateChanged(MicState state)
    {
        PostToUiThread(() =>
        {
            if (_activeMode == ClientMode.DualPc && !_settings.IsPaused)
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
            if (_activeMode == ClientMode.DualPc && !_settings.IsPaused)
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
            bool isSinglePc = _activeMode == ClientMode.SinglePc;
            using var icon = StatusIconGenerator.CreateStatusIcon(state, isServer: isSinglePc);
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = (Icon)icon.Clone();
            oldIcon?.Dispose();

            var statusText = state switch
            {
                MicState.Paused => "Paused",
                MicState.Disconnected => isSinglePc ? "Device Disconnected" : "Server Disconnected",
                MicState.Muted => "Microphone Muted",
                MicState.Unmuted => "Microphone Unmuted",
                _ => "Unknown"
            };

            var detailText = isSinglePc
                ? (!string.IsNullOrEmpty(_settings.MicrophoneName) ? $" - {_settings.MicrophoneName}" : "")
                : (!string.IsNullOrEmpty(_settings.ServerIp) ? $" - {_settings.ServerIp}" : "");
            _trayIcon.Text = TruncateText($"Mic Helper Client ({statusText}){detailText}", 63);
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
            _udpListener.Stop();
            _audioMonitor.StopMonitoring();
            _overlayForm.SetLiveState(MicState.Paused, isPaused: true);
            UpdateStatus(MicState.Paused);
        }
        else
        {
            if (_activeMode == ClientMode.SinglePc)
            {
                _audioMonitor.StartMonitoring(_settings.MicrophoneId, _settings.RetryTimeout);
                UpdateAudioState();
            }
            else
            {
                _overlayForm.SetLiveState(MicState.Disconnected, isPaused: false);
                UpdateStatus(MicState.Disconnected);
                _udpListener.Start(_settings.ServerIp, _settings.RetryTimeout);
            }
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
                _audioMonitor,
                _overlayForm,
                onModeChanged: newMode => SwitchMode(newMode),
                onMicrophoneChanged: (micId, micName) => SetMicrophone(micId, micName),
                onPortChanged: newPort =>
                {
                    if (_activeMode == ClientMode.DualPc)
                    {
                        _udpListener.Rebind(newPort, _settings.ServerIp);
                    }
                },
                onTimeoutChanged: newTimeout => SetRetryTimeout(newTimeout));

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();

            _overlayForm.Close();
            _overlayForm.Dispose();
            _assetManager.Dispose();
            _udpListener.Dispose();
            _audioMonitor.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void ExitThreadCore()
    {
        Dispose(true);
        base.ExitThreadCore();
    }
}
