using System.Net;
using System.Net.Sockets;
using MicHelper.Server.Config;
using MicHelper.Server.UI;
using MicHelper.Shared.Audio;
using MicHelper.Shared.Network;
using MicHelper.Shared.Protocol;
using MicHelper.Shared.UI;
using MicHelper.Shared.Common;

namespace MicHelper.Tests;

[TestClass]
public sealed class ServerNetworkingAndSettingsTests
{
    [TestMethod]
    public void ServerSettings_SaveAndLoad_RoundtripsSuccessfully()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "ServerSettingsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var original = new ServerSettings
            {
                RunOnStartup = true,
                MicrophoneId = "mic-12345",
                MicrophoneName = "HyperX QuadCast",
                Port = 13999,
                RetryTimeout = 8,
                IsPaused = true,
                DebugLogging = true
            };

            original.Save(tempFolder);

            var filePath = ServerSettings.GetFilePath(tempFolder);
            Assert.IsTrue(File.Exists(filePath));

            var loaded = ServerSettings.Load(tempFolder);
            Assert.AreEqual(original.RunOnStartup, loaded.RunOnStartup);
            Assert.AreEqual(original.MicrophoneId, loaded.MicrophoneId);
            Assert.AreEqual(original.MicrophoneName, loaded.MicrophoneName);
            Assert.AreEqual(original.Port, loaded.Port);
            Assert.AreEqual(original.RetryTimeout, loaded.RetryTimeout);
            Assert.AreEqual(original.IsPaused, loaded.IsPaused);
            Assert.AreEqual(original.DebugLogging, loaded.DebugLogging);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void NetworkUtils_DetectsBoundUdpPort()
    {
        // Bind an ephemeral UDP port
        using var testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        testSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var boundPort = ((IPEndPoint)testSocket.LocalEndPoint!).Port;

        // Port should be detected as in use
        var inUse = NetworkUtils.IsUdpPortInUse(boundPort);
        Assert.IsTrue(inUse);
    }

    [TestMethod]
    public async Task UdpBroadcaster_StateChange_EmitsBurstOfThreePackets()
    {
        int testPort = 13290;
        using var broadcaster = new UdpBroadcaster(testPort);

        var packetsSent = new List<StatusPacket>();
        broadcaster.PacketSent += packet =>
        {
            lock (packetsSent)
            {
                packetsSent.Add(packet);
            }
        };

        // Trigger state transition
        broadcaster.UpdateState(MicState.Muted, "Test Mic");

        // Wait up to 500ms for 3 burst packets
        var timeout = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < timeout)
        {
            lock (packetsSent)
            {
                if (packetsSent.Count >= 3) break;
            }
            await Task.Delay(20);
        }

        lock (packetsSent)
        {
            Assert.IsGreaterThanOrEqualTo(3, packetsSent.Count);
            Assert.IsTrue(packetsSent.All(p => p.State == MicState.Muted));
            Assert.IsTrue(packetsSent.All(p => p.Type == PacketType.StateChange));
        }
    }

    [TestMethod]
    public async Task UdpBroadcaster_SetPaused_EmitsPausedPacket()
    {
        int testPort = 13291;
        using var broadcaster = new UdpBroadcaster(testPort);

        var packetsSent = new List<StatusPacket>();
        broadcaster.PacketSent += packet =>
        {
            lock (packetsSent)
            {
                packetsSent.Add(packet);
            }
        };

        broadcaster.SetPaused(true);

        var timeout = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < timeout)
        {
            lock (packetsSent)
            {
                if (packetsSent.Any(p => p.State == MicState.Paused)) break;
            }
            await Task.Delay(20);
        }

        lock (packetsSent)
        {
            Assert.IsTrue(packetsSent.Any(p => p.State == MicState.Paused));
            Assert.IsTrue(broadcaster.IsPaused);
        }
    }

    [TestMethod]
    public void ServerSettingsForm_CanOpenAndHandleCreated_AfterIconDisposed()
    {
        // Simulate tray icon disposing the application icon
        var previousIcon = StatusIconGenerator.GetAppIcon();
        previousIcon.Dispose();

        var settings = new ServerSettings();
        using var audioMonitor = new WindowsAudioMonitor();
        using var form = new ServerSettingsForm(settings, audioMonitor, _ => {}, _ => {}, _ => {});

        _ = form.Handle;
        Assert.IsTrue(form.IsHandleCreated);
        Assert.IsNotNull(form.Icon);
        Assert.AreEqual(32, form.Icon.Width);
    }

    [TestMethod]
    public void ServerSettingsForm_DisplaysVersionLabelBetweenButtons()
    {
        var settings = new ServerSettings();
        using var audioMonitor = new WindowsAudioMonitor();
        using var form = new ServerSettingsForm(settings, audioMonitor, _ => {}, _ => {}, _ => {});
        _ = form.Handle;

        Assert.IsNotNull(form.VersionLabel);
        Assert.AreEqual(AppVersion.DisplayVersion, form.VersionLabel.Text);
        Assert.AreEqual(ContentAlignment.MiddleCenter, form.VersionLabel.TextAlign);
        Assert.AreEqual(SystemColors.GrayText, form.VersionLabel.ForeColor);
        Assert.IsTrue(form.Controls.Contains(form.VersionLabel));

        // Verify position is between left button (X=20, Width=100) and right button (X=280, Width=80) at Y=260
        Assert.AreEqual(260, form.VersionLabel.Location.Y);
        Assert.IsGreaterThanOrEqualTo(form.VersionLabel.Location.X, 120);
        Assert.IsLessThanOrEqualTo(form.VersionLabel.Right, 280);
    }

    [TestMethod]
    public void MicrophoneSelectionController_Populate_WithActiveDevice_SelectsDevice()
    {
        var fakeMonitor = new FakeAudioMonitor
        {
            Devices = new List<AudioDeviceInfo>
            {
                new("dev-1", "Microphone 1", true),
                new("dev-2", "Microphone 2", false)
            }
        };

        using var cbo = new ComboBox();
        MicComboItem? changedItem = null;
        var controller = new MicrophoneSelectionController(cbo, fakeMonitor, item => changedItem = item);

        controller.Populate("dev-2", "Microphone 2");

        Assert.AreEqual(2, cbo.Items.Count);
        Assert.IsNotNull(controller.SelectedItem);
        Assert.AreEqual("dev-2", controller.SelectedId);
        Assert.IsFalse(controller.SelectedItem.IsMissing);
    }

    [TestMethod]
    public void MicrophoneSelectionController_Populate_WithMissingDevice_InsertsMissingItemFirst()
    {
        var fakeMonitor = new FakeAudioMonitor
        {
            Devices = new List<AudioDeviceInfo>
            {
                new("dev-1", "Microphone 1", true)
            }
        };

        using var cbo = new ComboBox();
        MicComboItem? changedItem = null;
        var controller = new MicrophoneSelectionController(cbo, fakeMonitor, item => changedItem = item);

        controller.Populate("dev-lost", "Lost Microphone");

        Assert.AreEqual(2, cbo.Items.Count);
        Assert.IsNotNull(controller.SelectedItem);
        Assert.AreEqual("dev-lost", controller.SelectedId);
        Assert.IsTrue(controller.SelectedItem.IsMissing);
        Assert.AreEqual("Lost Microphone", controller.SelectedItem.DisplayName);
    }

    [TestMethod]
    public void MicrophoneSelectionController_Populate_NoSavedDevice_SelectsFirstAndTriggersCallback()
    {
        var fakeMonitor = new FakeAudioMonitor
        {
            Devices = new List<AudioDeviceInfo>
            {
                new("dev-1", "Microphone 1", true),
                new("dev-2", "Microphone 2", false)
            }
        };

        using var cbo = new ComboBox();
        MicComboItem? changedItem = null;
        var controller = new MicrophoneSelectionController(cbo, fakeMonitor, item => changedItem = item);

        controller.Populate(null, null);

        Assert.AreEqual(2, cbo.Items.Count);
        Assert.IsNotNull(controller.SelectedItem);
        Assert.AreEqual("dev-1", controller.SelectedId);
        Assert.IsNotNull(changedItem);
        Assert.AreEqual("dev-1", changedItem.Id);
    }

    private sealed class FakeAudioMonitor : IAudioMonitor
    {
        public List<AudioDeviceInfo> Devices { get; set; } = new();
        public IReadOnlyList<AudioDeviceInfo> GetActiveCaptureDevices() => Devices;
        public AudioDeviceInfo? GetDefaultCaptureDevice() => Devices.FirstOrDefault(d => d.IsDefault);
        public void StartMonitoring(string? targetDeviceId, int retryTimeoutSeconds = 5) { }
        public void StopMonitoring() { }
        public bool IsMuted => false;
        public bool IsConnected => true;
        public string? CurrentDeviceId => null;
        public string? CurrentDeviceName => null;
#pragma warning disable CS0067
        public event Action<bool>? MuteChanged;
        public event Action<bool>? ConnectionChanged;
        public event Action? DevicesChanged;
#pragma warning restore CS0067
        public void Dispose() { }
    }
}

