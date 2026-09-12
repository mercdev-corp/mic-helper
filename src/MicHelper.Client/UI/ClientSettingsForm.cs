using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MicHelper.Client.Config;
using MicHelper.Client.Overlay;
using MicHelper.Shared.Audio;
using MicHelper.Shared.Common;
using MicHelper.Shared.Network;
using MicHelper.Shared.UI;

namespace MicHelper.Client.UI;

public sealed class ClientSettingsForm : Form
{
    private readonly ClientSettings _settings;
    private readonly UdpListener _udpListener;
    private readonly IAudioMonitor _audioMonitor;
    private readonly OverlayForm _overlayForm;
    private readonly Action<ClientMode>? _onModeChanged;
    private readonly Action<string?, string>? _onMicrophoneChanged;
    private readonly Action<int>? _onPortChanged;
    private readonly Action<int>? _onTimeoutChanged;

    private ComboBox _cboMode = null!;
    private CheckBox _chkStartup = null!;
    private CheckBox _chkDebugLogging = null!;
    private Label _lblDualPcNote = null!;
    private Label _lblServer = null!;
    private ComboBox _cboServerIp = null!;
    private Label _lblPort = null!;
    private TextBox _txtPort = null!;
    private Label _lblMicrophone = null!;
    private ComboBox _cboMicrophone = null!;
    private MicrophoneSelectionController _micController = null!;
    private Label _lblTimeout = null!;
    private NumericUpDown _numTimeout = null!;
    private TrackBar _trkOpacity = null!;
    private Label _lblOpacityVal = null!;
    private TrackBar _trkFrequency = null!;
    private Label _lblFrequencyVal = null!;
    private TrackBar _trkSize = null!;
    private Label _lblSizeVal = null!;
    private Button _btnOpenLogs = null!;
    private Button _btnClose = null!;
    private Label _lblVersion = null!;
    private bool _isUpdatingControls;

    internal Label VersionLabel => _lblVersion;
    internal ComboBox ModeComboBox => _cboMode;
    internal Label DualPcNoteLabel => _lblDualPcNote;
    internal ComboBox ServerComboBox => _cboServerIp;
    internal TextBox PortTextBox => _txtPort;
    internal Label MicrophoneLabel => _lblMicrophone;
    internal ComboBox MicrophoneComboBox => _cboMicrophone;
    internal Label TimeoutLabel => _lblTimeout;
    internal NumericUpDown TimeoutNumeric => _numTimeout;
    internal MicrophoneSelectionController MicController => _micController;

    private record ServerComboItem(string? Ip, string DisplayText, bool IsOffline);

    public ClientSettingsForm(
        ClientSettings settings,
        UdpListener udpListener,
        OverlayForm overlayForm,
        Action<int> onPortChanged)
        : this(settings, udpListener, new WindowsAudioMonitor(), overlayForm, null, null, onPortChanged, null)
    {
    }

    public ClientSettingsForm(
        ClientSettings settings,
        UdpListener udpListener,
        IAudioMonitor audioMonitor,
        OverlayForm overlayForm,
        Action<ClientMode>? onModeChanged = null,
        Action<string?, string>? onMicrophoneChanged = null,
        Action<int>? onPortChanged = null,
        Action<int>? onTimeoutChanged = null)
    {
        _settings = settings;
        _udpListener = udpListener;
        _audioMonitor = audioMonitor;
        _overlayForm = overlayForm;
        _onModeChanged = onModeChanged;
        _onMicrophoneChanged = onMicrophoneChanged;
        _onPortChanged = onPortChanged;
        _onTimeoutChanged = onTimeoutChanged;

        InitializeComponent();
        LoadSettingsIntoControls();

        // Synchronize size slider when overlay is resized via mouse drag or scroll wheel
        _overlayForm.OverlayResized += OnOverlayResized;

        // Enable WYSIWYG mode while settings dialog is open
        _overlayForm.SetWysiwygMode(true);
    }

    private void InitializeComponent()
    {
        Text = "Mic Helper Client Settings";
        Icon = StatusIconGenerator.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 525);
        ShowInTaskbar = true;

        var lblMode = new Label
        {
            Text = "Mode:",
            Location = new Point(20, 10),
            AutoSize = true
        };

        _cboMode = new ComboBox
        {
            Location = new Point(20, 30),
            Width = 380,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboMode.Items.Add("Dual PC");
        _cboMode.Items.Add("Single PC");
        _cboMode.SelectedIndexChanged += CboMode_SelectedIndexChanged;

        _chkStartup = new CheckBox
        {
            Text = "Run on startup",
            Location = new Point(20, 60),
            AutoSize = true
        };
        _chkStartup.CheckedChanged += ChkStartup_CheckedChanged;

        _chkDebugLogging = new CheckBox
        {
            Text = "Enable debug logging",
            Location = new Point(180, 60),
            AutoSize = true
        };
        _chkDebugLogging.CheckedChanged += ChkDebugLogging_CheckedChanged;

        _lblDualPcNote = new Label
        {
            Text = "Run server app on remote PC where your Microphone is plugged in",
            Location = new Point(20, 85),
            MaximumSize = new Size(380, 0),
            ForeColor = SystemColors.GrayText,
            AutoSize = true
        };

        _lblServer = new Label
        {
            Text = "Server:",
            Location = new Point(20, 126),
            AutoSize = true
        };

        _cboServerIp = new ComboBox
        {
            Location = new Point(20, 148),
            Width = 380,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22
        };
        _cboServerIp.DrawItem += CboServerIp_DrawItem;
        _cboServerIp.SelectedIndexChanged += CboServerIp_SelectedIndexChanged;

        _lblPort = new Label
        {
            Text = "Port number:",
            Location = new Point(20, 180),
            AutoSize = true
        };

        _txtPort = new TextBox
        {
            Location = new Point(20, 202),
            Width = 100,
            Text = _settings.Port.ToString()
        };
        _txtPort.TextChanged += TxtPort_TextChanged;

        _lblMicrophone = new Label
        {
            Text = "Microphone:",
            Location = new Point(20, 88),
            AutoSize = true
        };

        _cboMicrophone = new ComboBox
        {
            Location = new Point(20, 110),
            Width = 380,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _micController = new MicrophoneSelectionController(_cboMicrophone, _audioMonitor, item =>
        {
            _settings.MicrophoneId = item.Id;
            _settings.MicrophoneName = item.DisplayName;
            _settings.Save();
            _onMicrophoneChanged?.Invoke(item.Id, item.DisplayName);
        });

        _lblTimeout = new Label
        {
            Text = "Retry timeout (seconds):",
            Location = new Point(160, 180),
            AutoSize = true
        };

        _numTimeout = new NumericUpDown
        {
            Location = new Point(160, 202),
            Width = 100,
            Minimum = 1,
            Maximum = 300,
            Value = _settings.RetryTimeout
        };
        _numTimeout.ValueChanged += NumTimeout_ValueChanged;

        var lblOpacity = new Label
        {
            Text = "Overlay maximum opacity:",
            Location = new Point(20, 245),
            AutoSize = true
        };
        _lblOpacityVal = new Label
        {
            Location = new Point(220, 245),
            Width = 50,
            Text = $"{_settings.Opacity}%"
        };
        _trkOpacity = new TrackBar
        {
            Location = new Point(20, 265),
            Width = 380,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = _settings.Opacity
        };
        _trkOpacity.ValueChanged += TrkOpacity_ValueChanged;

        var lblFreq = new Label
        {
            Text = "Pulse frequency (seconds):",
            Location = new Point(20, 310),
            AutoSize = true
        };
        _lblFrequencyVal = new Label
        {
            Location = new Point(220, 310),
            Width = 50,
            Text = $"{_settings.PulseFrequency:F1}s"
        };
        _trkFrequency = new TrackBar
        {
            Location = new Point(20, 330),
            Width = 380,
            Minimum = 1, // 0.1s
            Maximum = 50, // 5.0s
            TickFrequency = 5,
            Value = Math.Clamp((int)(_settings.PulseFrequency * 10), 1, 50)
        };
        _trkFrequency.ValueChanged += TrkFrequency_ValueChanged;

        var lblSize = new Label
        {
            Text = "Overlay size (pixels):",
            Location = new Point(20, 375),
            AutoSize = true
        };
        _lblSizeVal = new Label
        {
            Location = new Point(220, 375),
            Width = 60,
            Text = $"{_settings.OverlayWidth}px"
        };
        _trkSize = new TrackBar
        {
            Location = new Point(20, 395),
            Width = 380,
            Minimum = 32,
            Maximum = 1024,
            TickFrequency = 64,
            Value = Math.Clamp(_settings.OverlayWidth, 32, 1024)
        };
        _trkSize.ValueChanged += TrkSize_ValueChanged;

        var lblHint = new Label
        {
            Text = "Drag overlay to move • Scroll mouse wheel or drag corners to resize.",
            Location = new Point(20, 440),
            MaximumSize = new Size(380, 0),
            ForeColor = Color.Gray,
            AutoSize = true
        };

        _btnOpenLogs = new Button
        {
            Text = "View Logs...",
            Location = new Point(20, 475),
            Width = 100,
            Height = 30
        };
        _btnOpenLogs.Click += BtnOpenLogs_Click;

        _btnClose = new Button
        {
            Text = "Close",
            Location = new Point(300, 475),
            Width = 100,
            Height = 30
        };
        _btnClose.Click += (_, _) => Close();

        _lblVersion = new Label
        {
            Text = AppVersion.DisplayVersion,
            Location = new Point(120, 475),
            Size = new Size(180, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true
        };

        Controls.Add(lblMode);
        Controls.Add(_cboMode);
        Controls.Add(_chkStartup);
        Controls.Add(_chkDebugLogging);
        Controls.Add(_lblDualPcNote);
        Controls.Add(_lblServer);
        Controls.Add(_cboServerIp);
        Controls.Add(_lblPort);
        Controls.Add(_txtPort);
        Controls.Add(_lblMicrophone);
        Controls.Add(_cboMicrophone);
        Controls.Add(_lblTimeout);
        Controls.Add(_numTimeout);
        Controls.Add(lblOpacity);
        Controls.Add(_lblOpacityVal);
        Controls.Add(_trkOpacity);
        Controls.Add(lblFreq);
        Controls.Add(_lblFrequencyVal);
        Controls.Add(_trkFrequency);
        Controls.Add(lblSize);
        Controls.Add(_lblSizeVal);
        Controls.Add(_trkSize);
        Controls.Add(lblHint);
        Controls.Add(_btnOpenLogs);
        Controls.Add(_lblVersion);
        Controls.Add(_btnClose);

        _udpListener.DiscoveredServersUpdated += OnDiscoveredServersUpdated;
        _udpListener.TargetServerConnectionChanged += OnTargetServerConnectionChanged;
        _audioMonitor.DevicesChanged += OnAudioDevicesChanged;
        _cboServerIp.DropDownClosed += (_, _) => PopulateServerIpList();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_lblDualPcNote != null && _lblServer != null && _settings != null)
        {
            ApplyLayoutForMode(_settings.Mode);
        }
    }

    private void ApplyLayoutForMode(ClientMode mode)
    {
        bool isSinglePc = mode == ClientMode.SinglePc;

        // Dual PC controls visibility
        _lblDualPcNote.Visible = !isSinglePc;
        _lblServer.Visible = !isSinglePc;
        _cboServerIp.Visible = !isSinglePc;
        _lblPort.Visible = !isSinglePc;
        _txtPort.Visible = !isSinglePc;

        // Single PC controls visibility
        _lblMicrophone.Visible = isSinglePc;
        _cboMicrophone.Visible = isSinglePc;

        if (isSinglePc)
        {
            _lblTimeout.Text = "Microphone reconnect check (seconds):";
            _lblTimeout.Location = new Point(20, 155);
            _numTimeout.Location = new Point(20, 178);
        }
        else
        {
            int noteHeight = _lblDualPcNote.GetPreferredSize(new Size(_lblDualPcNote.MaximumSize.Width, 0)).Height;
            int effectiveNoteBottom = _lblDualPcNote.Location.Y + Math.Max(_lblDualPcNote.Height, noteHeight);
            int serverY = Math.Max(126, effectiveNoteBottom + 4);
            int deltaY = serverY - 126;

            _lblServer.Location = new Point(20, serverY);
            _cboServerIp.Location = new Point(20, 148 + deltaY);
            _lblPort.Location = new Point(20, 180 + deltaY);
            _txtPort.Location = new Point(20, 202 + deltaY);

            _lblTimeout.Text = "Retry timeout (seconds):";
            _lblTimeout.Location = new Point(160, 180 + deltaY);
            _numTimeout.Location = new Point(160, 202 + deltaY);
        }
    }

    private void LoadSettingsIntoControls()
    {
        _isUpdatingControls = true;
        try
        {
            _chkStartup.Checked = StartupRegistryManager.IsStartupEnabled("MicHelperClient");
            _chkDebugLogging.Checked = _settings.DebugLogging;
            _cboMode.SelectedIndex = _settings.Mode == ClientMode.SinglePc ? 1 : 0;
            _numTimeout.Value = Math.Clamp(_settings.RetryTimeout, 1, 300);
            _txtPort.Text = _settings.Port.ToString();

            ApplyLayoutForMode(_settings.Mode);

            if (_settings.Mode == ClientMode.SinglePc)
            {
                _micController.Populate(_settings.MicrophoneId, _settings.MicrophoneName);
            }
            else
            {
                PopulateServerIpList();
            }
        }
        finally
        {
            _isUpdatingControls = false;
        }
    }

    private void CboMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingControls) return;

        var selectedMode = _cboMode.SelectedIndex == 1 ? ClientMode.SinglePc : ClientMode.DualPc;
        if (_settings.Mode != selectedMode)
        {
            _settings.Mode = selectedMode;
            _settings.Save();
            ApplyLayoutForMode(selectedMode);

            if (selectedMode == ClientMode.SinglePc)
            {
                _micController.Populate(_settings.MicrophoneId, _settings.MicrophoneName);
            }
            else
            {
                PopulateServerIpList();
            }

            _onModeChanged?.Invoke(selectedMode);
        }
    }

    private void OnDiscoveredServersUpdated()
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(OnDiscoveredServersUpdated)); } catch { }
            return;
        }
        if (_cboServerIp.DroppedDown) return;
        if (_settings.Mode == ClientMode.DualPc)
        {
            PopulateServerIpList();
        }
    }

    private void OnTargetServerConnectionChanged(bool isConnected)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => OnTargetServerConnectionChanged(isConnected))); } catch { }
            return;
        }
        if (_cboServerIp.DroppedDown) return;
        if (_settings.Mode == ClientMode.DualPc)
        {
            PopulateServerIpList();
        }
    }

    private void OnAudioDevicesChanged()
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(OnAudioDevicesChanged)); } catch { }
            return;
        }
        if (_cboMicrophone.DroppedDown) return;
        if (_settings.Mode == ClientMode.SinglePc)
        {
            _micController.Populate(_settings.MicrophoneId, _settings.MicrophoneName);
        }
    }

    private void PopulateServerIpList()
    {
        _isUpdatingControls = true;
        try
        {
            var prevSelectedIp = (_cboServerIp.SelectedItem as ServerComboItem)?.Ip ?? _settings.ServerIp;

            _cboServerIp.BeginUpdate();
            _cboServerIp.Items.Clear();

            var servers = _udpListener.GetDiscoveredServers();
            string? savedIp = _settings.ServerIp;

            // Check if saved server matches any discovered server
            var targetDiscovered = !string.IsNullOrEmpty(savedIp)
                ? servers.FirstOrDefault(s => string.Equals(s.ServerIp, savedIp, StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(s.ServerId, savedIp, StringComparison.OrdinalIgnoreCase))
                : null;

            bool isTargetOnline = _udpListener.IsConnected && targetDiscovered != null;

            ServerComboItem? itemToSelect = null;

            if (!string.IsNullOrEmpty(savedIp))
            {
                string displayText;
                if (targetDiscovered != null)
                {
                    displayText = $"{targetDiscovered.ServerIp} - {targetDiscovered.HostName} ({targetDiscovered.MicrophoneName})";
                }
                else
                {
                    displayText = savedIp;
                }

                if (!isTargetOnline)
                {
                    displayText += " (Offline)";
                }

                var targetItem = new ServerComboItem(savedIp, displayText, IsOffline: !isTargetOnline);
                _cboServerIp.Items.Add(targetItem);

                if (string.Equals(prevSelectedIp, savedIp, StringComparison.OrdinalIgnoreCase))
                {
                    itemToSelect = targetItem;
                }
            }

            // Add other discovered servers
            foreach (var s in servers)
            {
                if (!string.IsNullOrEmpty(savedIp) &&
                    (string.Equals(s.ServerIp, savedIp, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(s.ServerId, savedIp, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                bool isOnline = (DateTime.UtcNow - s.LastSeen) <= TimeSpan.FromSeconds(_settings.RetryTimeout);
                var text = $"{s.ServerIp} - {s.HostName} ({s.MicrophoneName})" + (isOnline ? "" : " (Offline)");
                var item = new ServerComboItem(s.ServerIp, text, IsOffline: !isOnline);
                _cboServerIp.Items.Add(item);

                if (string.Equals(prevSelectedIp, s.ServerIp, StringComparison.OrdinalIgnoreCase))
                {
                    itemToSelect = item;
                }
            }

            if (itemToSelect != null)
            {
                _cboServerIp.SelectedItem = itemToSelect;
            }
            else if (_cboServerIp.Items.Count > 0)
            {
                _cboServerIp.SelectedIndex = 0;
            }

            _cboServerIp.Invalidate();
        }
        finally
        {
            _cboServerIp.EndUpdate();
            _isUpdatingControls = false;
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyTitleBarTheme();
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            // Set neutral title bar color matching SystemColors.Control (0x00F0F0F0) and black text (0x00000000)
            // This prevents Windows from overriding the caption with a bright accent color when ColorPrevalence is enabled.
            int captionColor = 0x00F0F0F0;
            int textColor = 0x00000000;
            DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
        }
        catch
        {
        }
    }

    private void CboServerIp_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _cboServerIp.Items.Count) return;

        e.DrawBackground();

        if (_cboServerIp.Items[e.Index] is ServerComboItem item)
        {
            using var brush = new SolidBrush(item.IsOffline ? Color.Red : e.ForeColor);
            var fontStyle = item.IsOffline ? (e.Font?.Style ?? FontStyle.Regular) | FontStyle.Strikeout : (e.Font?.Style ?? FontStyle.Regular);
            using var font = new Font(e.Font ?? SystemFonts.DefaultFont, fontStyle);

            e.Graphics.DrawString(item.DisplayText, font, brush, e.Bounds.X + 2, e.Bounds.Y + 2);
        }

        e.DrawFocusRectangle();
    }

    private void CboServerIp_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingControls) return;

        if (_cboServerIp.SelectedItem is ServerComboItem selected && !string.IsNullOrEmpty(selected.Ip))
        {
            if (!string.Equals(_settings.ServerIp, selected.Ip, StringComparison.OrdinalIgnoreCase))
            {
                _settings.ServerIp = selected.Ip;
                _settings.Save();
                _udpListener.SetTargetServer(selected.Ip);
            }
        }
    }

    private void ChkStartup_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingControls) return;

        try
        {
            StartupRegistryManager.SetStartupEnabled("MicHelperClient", _chkStartup.Checked);
            _settings.RunOnStartup = _chkStartup.Checked;
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update startup setting: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void TxtPort_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingControls) return;

        if (int.TryParse(_txtPort.Text.Trim(), out int port) && port is >= 1 and <= 65535)
        {
            _txtPort.ForeColor = SystemColors.WindowText;
            if (port != _settings.Port)
            {
                _settings.Port = port;
                _settings.Save();
                _onPortChanged?.Invoke(port);
            }
        }
        else
        {
            _txtPort.ForeColor = Color.Red;
        }
    }

    private void NumTimeout_ValueChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingControls) return;

        int val = (int)_numTimeout.Value;
        _settings.RetryTimeout = val;
        _settings.Save();
        if (_settings.Mode == ClientMode.DualPc)
        {
            _udpListener.SetRetryTimeout(val);
        }
        _onTimeoutChanged?.Invoke(val);
    }

    private void TrkOpacity_ValueChanged(object? sender, EventArgs e)
    {
        int val = _trkOpacity.Value;
        _lblOpacityVal.Text = $"{val}%";
        _settings.Opacity = val;
        _settings.Save();
        _overlayForm.UpdateAnimationSettings(val, _settings.PulseFrequency);
    }

    private void TrkFrequency_ValueChanged(object? sender, EventArgs e)
    {
        double freq = _trkFrequency.Value / 10.0;
        _lblFrequencyVal.Text = $"{freq:F1}s";
        _settings.PulseFrequency = freq;
        _settings.Save();
        _overlayForm.UpdateAnimationSettings(_settings.Opacity, freq);
    }

    private void TrkSize_ValueChanged(object? sender, EventArgs e)
    {
        int val = _trkSize.Value;
        _lblSizeVal.Text = $"{val}px";
        _overlayForm.ApplyNewSize(val);
    }

    private void OnOverlayResized(int newSize)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnOverlayResized(newSize)));
            return;
        }

        if (_trkSize.Value != newSize)
        {
            _trkSize.Value = Math.Clamp(newSize, 32, 1024);
        }
        _lblSizeVal.Text = $"{newSize}px";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _udpListener.DiscoveredServersUpdated -= OnDiscoveredServersUpdated;
        _udpListener.TargetServerConnectionChanged -= OnTargetServerConnectionChanged;
        _audioMonitor.DevicesChanged -= OnAudioDevicesChanged;
        _overlayForm.OverlayResized -= OnOverlayResized;
        _overlayForm.SetWysiwygMode(false);
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _udpListener.DiscoveredServersUpdated -= OnDiscoveredServersUpdated;
            _udpListener.TargetServerConnectionChanged -= OnTargetServerConnectionChanged;
            _audioMonitor.DevicesChanged -= OnAudioDevicesChanged;
            _overlayForm.OverlayResized -= OnOverlayResized;
            _overlayForm.SetWysiwygMode(false);
        }
        base.Dispose(disposing);
    }

    private void ChkDebugLogging_CheckedChanged(object? sender, EventArgs e)
    {
        _settings.DebugLogging = _chkDebugLogging.Checked;
        _settings.Save();
        AppLogger.IsDebugEnabled = _chkDebugLogging.Checked;
        AppLogger.Info($"Debug logging set to {_chkDebugLogging.Checked}");
    }

    private void BtnOpenLogs_Click(object? sender, EventArgs e)
    {
        try
        {
            string path = AppLogger.LogFilePath;
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mic-helper-client.log");
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, $"=== Mic Helper Client Log initialized on {DateTime.Now} ===" + Environment.NewLine);
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log file: {ex.Message}", "Open Log Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
