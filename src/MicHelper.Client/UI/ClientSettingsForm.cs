using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MicHelper.Client.Config;
using MicHelper.Client.Overlay;
using MicHelper.Shared.Common;
using MicHelper.Shared.Network;
using MicHelper.Shared.UI;

namespace MicHelper.Client.UI;

public sealed class ClientSettingsForm : Form
{
    private readonly ClientSettings _settings;
    private readonly UdpListener _udpListener;
    private readonly OverlayForm _overlayForm;
    private readonly Action<int> _onPortChanged;

    private CheckBox _chkStartup = null!;
    private CheckBox _chkDebugLogging = null!;
    private TextBox _txtPort = null!;
    private ComboBox _cboServerIp = null!;
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

    private record ServerComboItem(string? Ip, string DisplayText, bool IsOffline);

    public ClientSettingsForm(
        ClientSettings settings,
        UdpListener udpListener,
        OverlayForm overlayForm,
        Action<int> onPortChanged)
    {
        _settings = settings;
        _udpListener = udpListener;
        _overlayForm = overlayForm;
        _onPortChanged = onPortChanged;

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
        ClientSize = new Size(380, 525);
        ShowInTaskbar = true;

        _chkStartup = new CheckBox
        {
            Text = "Run on startup",
            Location = new Point(20, 20),
            AutoSize = true
        };
        _chkStartup.CheckedChanged += ChkStartup_CheckedChanged;

        _chkDebugLogging = new CheckBox
        {
            Text = "Enable debug logging",
            Location = new Point(160, 20),
            AutoSize = true
        };
        _chkDebugLogging.CheckedChanged += ChkDebugLogging_CheckedChanged;

        var lblServer = new Label
        {
            Text = "Server:",
            Location = new Point(20, 60),
            AutoSize = true
        };

        _cboServerIp = new ComboBox
        {
            Location = new Point(20, 85),
            Width = 340,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22
        };
        _cboServerIp.DrawItem += CboServerIp_DrawItem;
        _cboServerIp.SelectedIndexChanged += CboServerIp_SelectedIndexChanged;

        var lblTimeout = new Label
        {
            Text = "Retry timeout (seconds):",
            Location = new Point(20, 125),
            AutoSize = true
        };

        _numTimeout = new NumericUpDown
        {
            Location = new Point(20, 150),
            Width = 100,
            Minimum = 1,
            Maximum = 300,
            Value = _settings.RetryTimeout
        };
        _numTimeout.ValueChanged += NumTimeout_ValueChanged;

        var lblPort = new Label
        {
            Text = "Port number:",
            Location = new Point(20, 185),
            AutoSize = true
        };

        _txtPort = new TextBox
        {
            Location = new Point(20, 210),
            Width = 100,
            Text = _settings.Port.ToString()
        };
        _txtPort.TextChanged += TxtPort_TextChanged;

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
            Width = 340,
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
            Width = 340,
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
            Width = 340,
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
            Location = new Point(260, 475),
            Width = 100,
            Height = 30
        };
        _btnClose.Click += (_, _) => Close();

        _lblVersion = new Label
        {
            Text = AppVersion.DisplayVersion,
            Location = new Point(120, 475),
            Size = new Size(140, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true
        };

        Controls.Add(_chkStartup);
        Controls.Add(_chkDebugLogging);
        Controls.Add(lblServer);
        Controls.Add(_cboServerIp);
        Controls.Add(lblTimeout);
        Controls.Add(_numTimeout);
        Controls.Add(lblPort);
        Controls.Add(_txtPort);
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
        _cboServerIp.DropDownClosed += (_, _) => PopulateServerIpList();
    }

    private void LoadSettingsIntoControls()
    {
        _chkStartup.Checked = StartupRegistryManager.IsStartupEnabled("MicHelperClient");
        _chkDebugLogging.Checked = _settings.DebugLogging;
        PopulateServerIpList();
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
        PopulateServerIpList();
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
        PopulateServerIpList();
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
                _onPortChanged(port);
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
        _udpListener.SetRetryTimeout(val);
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
