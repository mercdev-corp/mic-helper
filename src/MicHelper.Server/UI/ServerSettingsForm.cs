using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MicHelper.Server.Config;
using MicHelper.Shared.Audio;
using MicHelper.Shared.Common;
using MicHelper.Shared.Network;
using MicHelper.Shared.UI;

namespace MicHelper.Server.UI;

public sealed class ServerSettingsForm : Form
{
    private readonly ServerSettings _settings;
    private readonly IAudioMonitor _audioMonitor;
    private readonly Action<int> _onPortChanged;
    private readonly Action<string?> _onMicrophoneChanged;
    private readonly Action<int> _onTimeoutChanged;

    private CheckBox _chkStartup = null!;
    private CheckBox _chkDebugLogging = null!;
    private ComboBox _cboMicrophone = null!;
    private NumericUpDown _numTimeout = null!;
    private TextBox _txtPort = null!;
    private ToolTip _toolTip = null!;
    private Button _btnOpenLogs = null!;
    private Button _btnClose = null!;
    private Label _lblVersion = null!;

    internal Label VersionLabel => _lblVersion;

    private record MicComboItem(string? Id, string DisplayName, bool IsMissing);

    public ServerSettingsForm(
        ServerSettings settings,
        IAudioMonitor audioMonitor,
        Action<int> onPortChanged,
        Action<string?> onMicrophoneChanged,
        Action<int> onTimeoutChanged)
    {
        _settings = settings;
        _audioMonitor = audioMonitor;
        _onPortChanged = onPortChanged;
        _onMicrophoneChanged = onMicrophoneChanged;
        _onTimeoutChanged = onTimeoutChanged;

        InitializeComponent();
        LoadSettingsIntoControls();
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

    private void InitializeComponent()
    {
        Text = "Mic Helper Server Settings";
        Icon = StatusIconGenerator.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 310);
        ShowInTaskbar = true;

        _toolTip = new ToolTip();

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

        var lblMic = new Label
        {
            Text = "Microphone:",
            Location = new Point(20, 60),
            AutoSize = true
        };

        _cboMicrophone = new ComboBox
        {
            Location = new Point(20, 85),
            Width = 340,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22
        };
        _cboMicrophone.DrawItem += CboMicrophone_DrawItem;
        _cboMicrophone.SelectedIndexChanged += CboMicrophone_SelectedIndexChanged;

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

        _btnOpenLogs = new Button
        {
            Text = "View Logs...",
            Location = new Point(20, 260),
            Width = 100,
            Height = 30
        };
        _btnOpenLogs.Click += BtnOpenLogs_Click;

        _btnClose = new Button
        {
            Text = "Close",
            Location = new Point(280, 260),
            Width = 80,
            Height = 30
        };
        _btnClose.Click += (_, _) => Close();

        _lblVersion = new Label
        {
            Text = AppVersion.DisplayVersion,
            Location = new Point(120, 260),
            Size = new Size(160, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true
        };

        Controls.Add(_chkStartup);
        Controls.Add(_chkDebugLogging);
        Controls.Add(lblMic);
        Controls.Add(_cboMicrophone);
        Controls.Add(lblTimeout);
        Controls.Add(_numTimeout);
        Controls.Add(lblPort);
        Controls.Add(_txtPort);
        Controls.Add(_btnOpenLogs);
        Controls.Add(_lblVersion);
        Controls.Add(_btnClose);
    }

    private void LoadSettingsIntoControls()
    {
        _chkStartup.Checked = StartupRegistryManager.IsStartupEnabled("MicHelperServer");
        _chkDebugLogging.Checked = _settings.DebugLogging;
        _numTimeout.Value = Math.Clamp(_settings.RetryTimeout, 1, 300);
        _txtPort.Text = _settings.Port.ToString();

        PopulateMicrophoneList();
    }

    private void PopulateMicrophoneList()
    {
        _cboMicrophone.Items.Clear();

        var activeDevices = _audioMonitor.GetActiveCaptureDevices();
        string? savedId = _settings.MicrophoneId;
        string? savedName = _settings.MicrophoneName;

        bool savedFound = false;
        if (!string.IsNullOrEmpty(savedId))
        {
            savedFound = activeDevices.Any(d => string.Equals(d.Id, savedId, StringComparison.OrdinalIgnoreCase));
        }

        // If saved mic is not found and was configured, add as first item with missing flag
        if (!savedFound && !string.IsNullOrEmpty(savedId))
        {
            var missingName = !string.IsNullOrEmpty(savedName) ? savedName : "(Missing Microphone)";
            var missingItem = new MicComboItem(savedId, missingName, IsMissing: true);
            _cboMicrophone.Items.Add(missingItem);
            _cboMicrophone.SelectedItem = missingItem;
        }

        // Add currently connected devices
        foreach (var dev in activeDevices)
        {
            var item = new MicComboItem(dev.Id, dev.Name + (dev.IsDefault ? " (Default)" : ""), IsMissing: false);
            _cboMicrophone.Items.Add(item);

            if (savedFound && string.Equals(dev.Id, savedId, StringComparison.OrdinalIgnoreCase))
            {
                _cboMicrophone.SelectedItem = item;
            }
        }

        // If nothing selected yet, select first or default
        if (_cboMicrophone.SelectedItem == null && _cboMicrophone.Items.Count > 0)
        {
            _cboMicrophone.SelectedIndex = 0;
            if (_cboMicrophone.SelectedItem is MicComboItem selectedItem)
            {
                _settings.MicrophoneId = selectedItem.Id;
                _settings.MicrophoneName = selectedItem.DisplayName;
                _settings.Save();
            }
        }
    }

    private void CboMicrophone_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _cboMicrophone.Items.Count) return;

        e.DrawBackground();

        if (_cboMicrophone.Items[e.Index] is MicComboItem item)
        {
            using var brush = new SolidBrush(item.IsMissing ? Color.Red : e.ForeColor);
            var fontStyle = item.IsMissing ? (e.Font?.Style ?? FontStyle.Regular) | FontStyle.Strikeout : (e.Font?.Style ?? FontStyle.Regular);
            using var font = new Font(e.Font ?? SystemFonts.DefaultFont, fontStyle);

            e.Graphics.DrawString(item.DisplayName, font, brush, e.Bounds.X + 2, e.Bounds.Y + 2);
        }

        e.DrawFocusRectangle();
    }

    private void CboMicrophone_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboMicrophone.SelectedItem is MicComboItem selectedItem)
        {
            if (!selectedItem.IsMissing)
            {
                _settings.MicrophoneId = selectedItem.Id;
                _settings.MicrophoneName = selectedItem.DisplayName;
                _settings.Save();
                _onMicrophoneChanged(selectedItem.Id);
            }
        }
    }

    private void ChkStartup_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            StartupRegistryManager.SetStartupEnabled("MicHelperServer", _chkStartup.Checked);
            _settings.RunOnStartup = _chkStartup.Checked;
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update startup setting: {ex.Message}", "Startup Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void NumTimeout_ValueChanged(object? sender, EventArgs e)
    {
        int val = (int)_numTimeout.Value;
        _settings.RetryTimeout = val;
        _settings.Save();
        _onTimeoutChanged(val);
    }

    private void TxtPort_TextChanged(object? sender, EventArgs e)
    {
        if (int.TryParse(_txtPort.Text.Trim(), out int port) && port is >= 1 and <= 65535)
        {
            bool inUse = (port != _settings.Port) && NetworkUtils.IsUdpPortInUse(port);
            if (inUse)
            {
                _txtPort.ForeColor = Color.Red;
                _toolTip.SetToolTip(_txtPort, "Port is already in use");
            }
            else
            {
                _txtPort.ForeColor = SystemColors.WindowText;
                _toolTip.SetToolTip(_txtPort, string.Empty);

                if (port != _settings.Port)
                {
                    _settings.Port = port;
                    _settings.Save();
                    _onPortChanged(port);
                }
            }
        }
        else
        {
            _txtPort.ForeColor = Color.Red;
            _toolTip.SetToolTip(_txtPort, "Invalid port number (1-65535)");
        }
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
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mic-helper-server.log");
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, $"=== Mic Helper Server Log initialized on {DateTime.Now} ===" + Environment.NewLine);
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log file: {ex.Message}", "Open Log Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
