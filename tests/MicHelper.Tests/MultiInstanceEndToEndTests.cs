using Microsoft.Win32;
using MicHelper.Client.Config;
using MicHelper.Server.Config;
using MicHelper.Shared.Common;
using MicHelper.Shared.Network;
using MicHelper.Shared.Protocol;

namespace MicHelper.Tests;

[TestClass]
public sealed class MultiInstanceEndToEndTests
{
    private const string TestRegistryKey = @"Software\MicHelperTest_MultiInstance";

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(TestRegistryKey, throwOnMissingSubKey: false);
        }
        catch
        {
        }
    }

    [TestMethod]
    public async Task MultiInstance_TwoServersAndTwoClients_SimultaneousIsolatedExecution()
    {
        var dir1 = Path.Combine(Path.GetTempPath(), "MultiInst1_" + Guid.NewGuid().ToString("N"));
        var dir2 = Path.Combine(Path.GetTempPath(), "MultiInst2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        try
        {
            int port1 = 13301;
            int port2 = 13302;

            // 1. Settings Isolation
            var serverSettings1 = new ServerSettings
            {
                Port = port1,
                MicrophoneId = "mic-guid-alpha",
                MicrophoneName = "Microphone Alpha"
            };
            serverSettings1.Save(dir1);

            var serverSettings2 = new ServerSettings
            {
                Port = port2,
                MicrophoneId = "mic-guid-beta",
                MicrophoneName = "Microphone Beta"
            };
            serverSettings2.Save(dir2);

            var loadedServer1 = ServerSettings.Load(dir1);
            var loadedServer2 = ServerSettings.Load(dir2);

            Assert.AreEqual("mic-guid-alpha", loadedServer1.MicrophoneId);
            Assert.AreEqual("mic-guid-beta", loadedServer2.MicrophoneId);
            Assert.AreEqual(port1, loadedServer1.Port);
            Assert.AreEqual(port2, loadedServer2.Port);

            // Client Settings Isolation
            var clientSettings1 = new ClientSettings
            {
                Port = port1,
                OverlayCenterX = 150,
                OverlayCenterY = 150,
                OverlayWidth = 64,
                OverlayHeight = 64
            };
            clientSettings1.Save(dir1);

            var clientSettings2 = new ClientSettings
            {
                Port = port2,
                OverlayCenterX = 900,
                OverlayCenterY = 700,
                OverlayWidth = 128,
                OverlayHeight = 128
            };
            clientSettings2.Save(dir2);

            var loadedClient1 = ClientSettings.Load(dir1);
            var loadedClient2 = ClientSettings.Load(dir2);

            Assert.AreEqual(150, loadedClient1.OverlayCenterX);
            Assert.AreEqual(900, loadedClient2.OverlayCenterX);

            // 2. Folder-scoped Mutex Isolation: Both instances run concurrently from different folders
            using var mutexServer1 = SingleInstanceMutex.TryAcquire("MicHelperServer", dir1);
            using var mutexServer2 = SingleInstanceMutex.TryAcquire("MicHelperServer", dir2);
            using var mutexClient1 = SingleInstanceMutex.TryAcquire("MicHelperClient", dir1);
            using var mutexClient2 = SingleInstanceMutex.TryAcquire("MicHelperClient", dir2);

            Assert.IsNotNull(mutexServer1);
            Assert.IsNotNull(mutexServer2);
            Assert.IsNotNull(mutexClient1);
            Assert.IsNotNull(mutexClient2);

            Assert.AreNotEqual(mutexServer1.MutexName, mutexServer2.MutexName);
            Assert.AreNotEqual(mutexClient1.MutexName, mutexClient2.MutexName);

            // 3. Network Isolation on Distinct Ports
            using var broadcaster1 = new UdpBroadcaster(port1, "server-alpha") { MicrophoneName = "Microphone Alpha" };
            using var broadcaster2 = new UdpBroadcaster(port2, "server-beta") { MicrophoneName = "Microphone Beta" };

            using var listener1 = new UdpListener(port1);
            using var listener2 = new UdpListener(port2);

            listener1.Start();
            listener2.Start();

            broadcaster1.Start();
            broadcaster2.Start();

            // Broadcaster 1 sends Muted
            broadcaster1.UpdateState(MicState.Muted);

            // Broadcaster 2 sends Unmuted
            broadcaster2.UpdateState(MicState.Unmuted, force: true);

            var timeout = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < timeout)
            {
                if (listener1.IsConnected && listener2.IsConnected) break;
                await Task.Delay(25);
            }

            Assert.IsTrue(listener1.IsConnected);
            Assert.IsTrue(listener2.IsConnected);

            Assert.AreEqual(MicState.Muted, listener1.LastReportedState);
            Assert.AreEqual(MicState.Unmuted, listener2.LastReportedState);
        }
        finally
        {
            if (Directory.Exists(dir1)) Directory.Delete(dir1, true);
            if (Directory.Exists(dir2)) Directory.Delete(dir2, true);
        }
    }

    [TestMethod]
    public void MultiInstance_AutoStartupRegistry_CoexistAndManageIndependently()
    {
        var exeServer1 = @"C:\Apps\Folder1\MicHelper.Server.exe";
        var exeServer2 = @"C:\Apps\Folder2\MicHelper.Server.exe";
        var exeClient1 = @"C:\Apps\Folder1\MicHelper.Client.exe";
        var exeClient2 = @"C:\Apps\Folder2\MicHelper.Client.exe";

        // Enable all 4 entries
        StartupRegistryManager.SetStartupEnabled("MicHelperServer", true, exeServer1, TestRegistryKey);
        StartupRegistryManager.SetStartupEnabled("MicHelperServer", true, exeServer2, TestRegistryKey);
        StartupRegistryManager.SetStartupEnabled("MicHelperClient", true, exeClient1, TestRegistryKey);
        StartupRegistryManager.SetStartupEnabled("MicHelperClient", true, exeClient2, TestRegistryKey);

        // Verify all 4 are enabled
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeServer1, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeServer2, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperClient", exeClient1, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperClient", exeClient2, TestRegistryKey));

        // Disable Server 1 only
        StartupRegistryManager.SetStartupEnabled("MicHelperServer", false, exeServer1, TestRegistryKey);

        // Verify Server 1 is removed, but Server 2, Client 1, and Client 2 remain enabled
        Assert.IsFalse(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeServer1, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeServer2, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperClient", exeClient1, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperClient", exeClient2, TestRegistryKey));

        // Disable Client 2 only
        StartupRegistryManager.SetStartupEnabled("MicHelperClient", false, exeClient2, TestRegistryKey);

        Assert.IsFalse(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeServer1, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeServer2, TestRegistryKey));
        Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperClient", exeClient1, TestRegistryKey));
        Assert.IsFalse(StartupRegistryManager.IsStartupEnabled("MicHelperClient", exeClient2, TestRegistryKey));
    }
}
