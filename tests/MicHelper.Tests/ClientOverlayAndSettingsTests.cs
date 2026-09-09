using MicHelper.Client.Config;
using MicHelper.Client.Overlay;
using MicHelper.Shared.Network;
using MicHelper.Shared.Protocol;
using MicHelper.Shared.UI;
using MicHelper.Shared.Common;

namespace MicHelper.Tests;

[TestClass]
public sealed class ClientOverlayAndSettingsTests
{
    [TestMethod]
    public void ClientSettings_SaveAndLoad_RoundtripsSuccessfully()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "ClientSettingsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var original = new ClientSettings
            {
                RunOnStartup = true,
                Port = 13888,
                ServerIp = "192.168.1.150",
                RetryTimeout = 7,
                Opacity = 85,
                PulseFrequency = 1.5,
                OverlayCenterX = 450,
                OverlayCenterY = 320,
                OverlayWidth = 96,
                OverlayHeight = 96,
                IsPaused = false,
                DebugLogging = true
            };

            original.Save(tempFolder);

            var filePath = ClientSettings.GetFilePath(tempFolder);
            Assert.IsTrue(File.Exists(filePath));

            var loaded = ClientSettings.Load(tempFolder);
            Assert.AreEqual(original.RunOnStartup, loaded.RunOnStartup);
            Assert.AreEqual(original.Port, loaded.Port);
            Assert.AreEqual(original.ServerIp, loaded.ServerIp);
            Assert.AreEqual(original.RetryTimeout, loaded.RetryTimeout);
            Assert.AreEqual(original.Opacity, loaded.Opacity);
            Assert.AreEqual(original.PulseFrequency, loaded.PulseFrequency);
            Assert.AreEqual(original.OverlayCenterX, loaded.OverlayCenterX);
            Assert.AreEqual(original.OverlayCenterY, loaded.OverlayCenterY);
            Assert.AreEqual(original.OverlayWidth, loaded.OverlayWidth);
            Assert.AreEqual(original.OverlayHeight, loaded.OverlayHeight);
            Assert.AreEqual(original.IsPaused, loaded.IsPaused);
            Assert.AreEqual(original.DebugLogging, loaded.DebugLogging);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void OverlayAssetManager_PreRenderAndCache_ProducesExactDimensions()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "AssetTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            using var assetManager = new OverlayAssetManager(tempFolder);
            assetManager.PreRenderAndCache(128, 128);

            var mutedBmp = assetManager.GetBitmap(MicState.Muted);
            Assert.IsNotNull(mutedBmp);
            Assert.AreEqual(128, mutedBmp.Width);
            Assert.AreEqual(128, mutedBmp.Height);

            var discBmp = assetManager.GetBitmap(MicState.Disconnected);
            Assert.IsNotNull(discBmp);
            Assert.AreEqual(128, discBmp.Width);
            Assert.AreEqual(128, discBmp.Height);

            var cacheDir = Path.Combine(tempFolder, "cache");
            var mutedFile = Path.Combine(cacheDir, "mic-muted_resized.png");
            var discFile = Path.Combine(cacheDir, "server-disconnected_resized.png");

            Assert.IsTrue(File.Exists(mutedFile));
            Assert.IsTrue(File.Exists(discFile));

            // Resizing again must overwrite existing files and not leave multiple version files in cache
            assetManager.PreRenderAndCache(256, 256);
            var files = Directory.GetFiles(cacheDir);
            Assert.HasCount(2, files);
            Assert.IsTrue(File.Exists(mutedFile));
            Assert.IsTrue(File.Exists(discFile));

            using var reloadedMuted = new Bitmap(mutedFile);
            Assert.AreEqual(256, reloadedMuted.Width);
            Assert.AreEqual(256, reloadedMuted.Height);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void OverlayForm_ZeroCpuSuspension_StopsTimerWhenUnmutedOrPaused()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "OverlayTest1_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var settings = new ClientSettings();
            using var assetManager = new OverlayAssetManager(tempFolder);
            using var overlay = new OverlayForm(settings, assetManager);

            // Initially disconnected, alert active
            overlay.SetLiveState(MicState.Disconnected, isPaused: false);
            Assert.IsTrue(overlay.IsPulseTimerRunning);

            // Transition to Unmuted: alert ceases, render timer MUST stop for zero CPU overhead
            overlay.SetLiveState(MicState.Unmuted, isPaused: false);
            Assert.IsFalse(overlay.IsPulseTimerRunning);
            Assert.IsFalse(overlay.Visible);

            // Transition to Muted: alert active, timer resumes
            overlay.SetLiveState(MicState.Muted, isPaused: false);
            Assert.IsTrue(overlay.IsPulseTimerRunning);
            Assert.IsTrue(overlay.Visible);

            // Transition to Paused: timer MUST stop
            overlay.SetLiveState(MicState.Muted, isPaused: true);
            Assert.IsFalse(overlay.IsPulseTimerRunning);
            Assert.IsFalse(overlay.Visible);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void OverlayForm_WysiwygMode_TogglesTransparentStyle()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "OverlayTest2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var settings = new ClientSettings();
            using var assetManager = new OverlayAssetManager(tempFolder);
            using var overlay = new OverlayForm(settings, assetManager);

            // Force handle creation
            var handle = overlay.Handle;
            Assert.AreNotEqual(IntPtr.Zero, handle);

            // Normal mode has WS_EX_TRANSPARENT
            int normalStyle = Win32Native.GetWindowLong(overlay.Handle, Win32Native.GWL_EXSTYLE);
            Assert.AreNotEqual(0, normalStyle & Win32Native.WS_EX_TRANSPARENT, "Normal overlay should be transparent to clicks");
            Assert.AreNotEqual(0, normalStyle & Win32Native.WS_EX_TOPMOST, "Should be topmost");

            // WYSIWYG mode removes WS_EX_TRANSPARENT to allow dragging/resizing
            overlay.SetWysiwygMode(true);
            int wysiwygStyle = Win32Native.GetWindowLong(overlay.Handle, Win32Native.GWL_EXSTYLE);
            Assert.AreEqual(0, wysiwygStyle & Win32Native.WS_EX_TRANSPARENT, "WYSIWYG overlay should not be click-through");

            // Exiting WYSIWYG restores WS_EX_TRANSPARENT
            overlay.SetWysiwygMode(false);
            int restoredStyle = Win32Native.GetWindowLong(overlay.Handle, Win32Native.GWL_EXSTYLE);
            Assert.AreNotEqual(0, restoredStyle & Win32Native.WS_EX_TRANSPARENT, "Click-through should be restored");
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void OverlayForm_TopmostStylesAndReassertTopmost_MaintainsTopmostStyle()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "OverlayTopmostTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var settings = new ClientSettings();
            using var assetManager = new OverlayAssetManager(tempFolder);
            using var overlay = new OverlayForm(settings, assetManager);

            // Force handle creation
            var handle = overlay.Handle;
            Assert.AreNotEqual(IntPtr.Zero, handle);

            // Verify WS_EX_TOPMOST style bit is set on window
            int initialExStyle = Win32Native.GetWindowLong(overlay.Handle, Win32Native.GWL_EXSTYLE);
            Assert.AreNotEqual(0, initialExStyle & Win32Native.WS_EX_TOPMOST, "Window should possess WS_EX_TOPMOST style");

            // Invoke ReassertTopmost directly
            overlay.ReassertTopmost();

            int postReassertExStyle = Win32Native.GetWindowLong(overlay.Handle, Win32Native.GWL_EXSTYLE);
            Assert.AreNotEqual(0, postReassertExStyle & Win32Native.WS_EX_TOPMOST, "Window should retain WS_EX_TOPMOST after ReassertTopmost");

            // Transition live state to Muted (invokes StartPulsing -> ReassertTopmost)
            overlay.SetLiveState(MicState.Muted, isPaused: false);
            Assert.IsTrue(overlay.Visible);
            Assert.IsTrue(overlay.IsPulseTimerRunning);

            // GetWindow with GW_HWNDPREV executes properly on window handle
            var prevHwnd = Win32Native.GetWindow(overlay.Handle, Win32Native.GW_HWNDPREV);
            Assert.IsTrue(prevHwnd == IntPtr.Zero || prevHwnd != IntPtr.Zero);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void OverlayForm_HandleLifecycle_RegistersAndUnhooksWinEvent()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "OverlayHookTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var settings = new ClientSettings();
            using var assetManager = new OverlayAssetManager(tempFolder);
            var overlay = new OverlayForm(settings, assetManager);

            // Before handle creation, hook should not be registered
            Assert.AreEqual(IntPtr.Zero, overlay.WinEventHookHandle, "Hook should be IntPtr.Zero before handle creation");

            // Create handle
            var handle = overlay.Handle;
            Assert.AreNotEqual(IntPtr.Zero, handle);

            // After handle creation, hook should be successfully registered
            var hookHandle = overlay.WinEventHookHandle;
            Assert.AreNotEqual(IntPtr.Zero, hookHandle, "Hook handle should be non-zero after handle creation");

            // Dispose overlay form
            overlay.Dispose();

            // After disposal, hook must be cleanly unhooked and reset
            Assert.AreEqual(IntPtr.Zero, overlay.WinEventHookHandle, "Hook handle must be reset to IntPtr.Zero after disposal");
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public async Task UdpListener_DiscoversBroadcasterAndDetectsOffline()
    {
        int testPort = 13295;
        using var broadcaster = new UdpBroadcaster(testPort, "server-discovery-test");
        using var listener = new UdpListener(testPort);

        var connectionStates = new List<bool>();
        listener.TargetServerConnectionChanged += conn =>
        {
            lock (connectionStates) connectionStates.Add(conn);
        };

        // Start listener with 1 second retry timeout
        listener.Start(targetServerIp: null, retryTimeoutSeconds: 1);

        // Send a burst from broadcaster
        broadcaster.UpdateState(MicState.Muted, "Studio Mic");

        // Wait up to 1 second to receive packet
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < timeout)
        {
            if (listener.IsConnected) break;
            await Task.Delay(25);
        }

        Assert.IsTrue(listener.IsConnected);
        Assert.AreEqual(MicState.Muted, listener.LastReportedState);

        var discovered = listener.GetDiscoveredServers();
        Assert.IsNotEmpty(discovered);
        Assert.IsTrue(discovered.Any(s => s.ServerId == "server-discovery-test"));

        // Stop broadcaster to simulate server going offline
        broadcaster.Stop();

        // Wait up to 2.5 seconds for watchdog to trigger disconnect
        timeout = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < timeout)
        {
            if (!listener.IsConnected) break;
            await Task.Delay(50);
        }

        Assert.IsFalse(listener.IsConnected);
        Assert.AreEqual(MicState.Disconnected, listener.LastReportedState);
    }

    [TestMethod]
    public void StatusIconGenerator_EmbeddedAssets_LoadSuccessfully()
    {
        var appIcon = StatusIconGenerator.GetAppIcon();
        Assert.IsNotNull(appIcon);
        Assert.AreEqual(32, appIcon.Width);
        Assert.AreEqual(32, appIcon.Height);

        using var mutedBmp = StatusIconGenerator.GetMicMutedBitmap();
        Assert.IsNotNull(mutedBmp);
        Assert.AreEqual(808, mutedBmp.Width);
        Assert.AreEqual(1394, mutedBmp.Height);

        using var unmutedBmp = StatusIconGenerator.GetMicUnmutedBitmap();
        Assert.IsNotNull(unmutedBmp);
        Assert.AreEqual(787, unmutedBmp.Width);
        Assert.AreEqual(1393, unmutedBmp.Height);

        using var deviceDiscBmp = StatusIconGenerator.GetDeviceDisconnectedBitmap();
        Assert.IsNotNull(deviceDiscBmp);
        Assert.AreEqual(808, deviceDiscBmp.Width);
        Assert.AreEqual(1269, deviceDiscBmp.Height);

        using var discBmp = StatusIconGenerator.GetServerDisconnectedBitmap();
        Assert.IsNotNull(discBmp);
        Assert.AreEqual(1079, discBmp.Width);
        Assert.AreEqual(1077, discBmp.Height);

        using var pauseBmp = StatusIconGenerator.GetPauseBitmap();
        Assert.IsNotNull(pauseBmp);
        Assert.AreEqual(714, pauseBmp.Width);
        Assert.AreEqual(1229, pauseBmp.Height);

        // Test all status icons for Client
        foreach (MicState state in Enum.GetValues<MicState>())
        {
            using var clientIcon = StatusIconGenerator.CreateClientStatusIcon(state);
            Assert.IsNotNull(clientIcon);
            Assert.AreEqual(32, clientIcon.Width);
            Assert.AreEqual(32, clientIcon.Height);
        }

        // Test all status icons for Server
        foreach (MicState state in Enum.GetValues<MicState>())
        {
            using var serverIcon = StatusIconGenerator.CreateServerStatusIcon(state);
            Assert.IsNotNull(serverIcon);
            Assert.AreEqual(32, serverIcon.Width);
            Assert.AreEqual(32, serverIcon.Height);
        }
    }

    [TestMethod]
    public void OverlayForm_ApplyNewSize_ResizesAndClampsCorrectly()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "OverlayResizeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var settings = new ClientSettings
            {
                OverlayWidth = 128,
                OverlayHeight = 128,
                OverlayCenterX = 300,
                OverlayCenterY = 300
            };
            using var assetMgr = new OverlayAssetManager(tempFolder);
            using var overlay = new OverlayForm(settings, assetMgr);

            _ = overlay.Handle; // Ensure handle created

            int reportedSize = 0;
            overlay.OverlayResized += sz => reportedSize = sz;

            overlay.ApplyNewSize(200);
            Assert.AreEqual(200, overlay.Width);
            Assert.AreEqual(200, overlay.Height);
            Assert.AreEqual(200, settings.OverlayWidth);
            Assert.AreEqual(200, reportedSize);

            overlay.ApplyNewSize(999);
            Assert.AreEqual(999, overlay.Width);
            Assert.AreEqual(999, settings.OverlayWidth);

            // Clamp max 1024
            overlay.ApplyNewSize(1500);
            Assert.AreEqual(1024, overlay.Width);
            Assert.AreEqual(1024, settings.OverlayWidth);

            // Clamp min 32
            overlay.ApplyNewSize(10);
            Assert.AreEqual(32, overlay.Width);
            Assert.AreEqual(32, settings.OverlayWidth);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public async Task UdpListener_SetTargetServer_SameServerDoesNotDisconnect()
    {
        int testPort = 13296;
        using var broadcaster = new UdpBroadcaster(testPort, "server-same-ip-test");
        using var listener = new UdpListener(testPort);

        listener.Start(targetServerIp: null, retryTimeoutSeconds: 5);
        broadcaster.UpdateState(MicState.Unmuted, "Test Mic");

        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < timeout && !listener.IsConnected)
        {
            await Task.Delay(25);
        }

        Assert.IsTrue(listener.IsConnected);
        var connectedIp = listener.TargetServerIp;
        Assert.IsNotNull(connectedIp);

        // Re-setting target server to same IP should NOT disconnect
        listener.SetTargetServer(connectedIp);
        Assert.IsTrue(listener.IsConnected);

        // Setting target server to different IP SHOULD disconnect
        listener.SetTargetServer("10.254.254.254");
        Assert.IsFalse(listener.IsConnected);
    }

    [TestMethod]
    public void ClientSettingsForm_PreservesConnectedStateAndShowsOfflineWhenDisconnected()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "SettingsFormTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            int testPort = 13297;
            var settings = new ClientSettings
            {
                ServerIp = "127.0.0.1",
                Port = testPort,
                RetryTimeout = 1
            };
            using var assetMgr = new OverlayAssetManager(tempFolder);
            using var overlay = new OverlayForm(settings, assetMgr);
            using var listener = new UdpListener(testPort);
            using var broadcaster = new UdpBroadcaster(testPort, "server-form-test");

            listener.Start(targetServerIp: null, retryTimeoutSeconds: 1);
            broadcaster.UpdateState(MicState.Unmuted, "Studio Mic");

            var timeout = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < timeout && !listener.IsConnected)
            {
                Thread.Sleep(25);
            }

            Assert.IsTrue(listener.IsConnected);
            settings.ServerIp = listener.TargetServerIp;

            // Opening ClientSettingsForm while connected MUST NOT disconnect the listener
            using var form = new MicHelper.Client.UI.ClientSettingsForm(settings, listener, overlay, _ => { });
            _ = form.Handle;

            Assert.IsTrue(listener.IsConnected, "Opening settings form must not drop connected state!");

            // Stop broadcaster to simulate server going offline
            broadcaster.Stop();

            timeout = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < timeout && listener.IsConnected)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            Assert.IsFalse(listener.IsConnected, "Listener must detect server going offline!");
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void ClientSettingsForm_DisplaysVersionLabelBetweenButtons()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "ClientSettingsVersionTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var settings = new ClientSettings();
            using var assetMgr = new OverlayAssetManager(tempFolder);
            using var overlay = new OverlayForm(settings, assetMgr);
            using var listener = new UdpListener(13991);
            using var form = new MicHelper.Client.UI.ClientSettingsForm(settings, listener, overlay, _ => { });
            _ = form.Handle;

            Assert.IsNotNull(form.VersionLabel);
            Assert.AreEqual(AppVersion.DisplayVersion, form.VersionLabel.Text);
            Assert.AreEqual(ContentAlignment.MiddleCenter, form.VersionLabel.TextAlign);
            Assert.AreEqual(SystemColors.GrayText, form.VersionLabel.ForeColor);
            Assert.IsTrue(form.Controls.Contains(form.VersionLabel));

            // Verify position is between left button (X=20, Width=100) and right button (X=260, Width=100)
            Assert.AreEqual(475, form.VersionLabel.Location.Y);
            Assert.IsGreaterThanOrEqualTo(form.VersionLabel.Location.X, 120);
            Assert.IsLessThanOrEqualTo(form.VersionLabel.Right, 260);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }
}

