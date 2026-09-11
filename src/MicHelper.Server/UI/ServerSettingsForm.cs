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
    private MicrophoneSelectionController _micController = null!;

    internal Label VersionLabel => _lblVersion;

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
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _micController = new MicrophoneSelectionController(_cboMicrophone, _audioMonitor, item =>
        {
            _settings.MicrophoneId = item.Id;
            _settings.MicrophoneName = item.DisplayName;
            _settings.Save();
            _onMicrophoneChanged(item.Id);
        });

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

        _micController.Populate(_settings.MicrophoneId, _settings.MicrophoneName);
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
