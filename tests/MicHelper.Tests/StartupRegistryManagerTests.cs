using Microsoft.Win32;
using MicHelper.Shared.Common;

namespace MicHelper.Tests;

[TestClass]
public sealed class StartupRegistryManagerTests
{
    [TestMethod]
    public void GetKeyName_DerivesFromExecutableDirectory()
    {
        var exe1 = @"C:\Games\Mic1\MicHelper.Server.exe";
        var exe2 = @"C:\Games\Mic2\MicHelper.Server.exe";

        var key1 = StartupRegistryManager.GetKeyName("MicHelperServer", exe1);
        var key2 = StartupRegistryManager.GetKeyName("MicHelperServer", exe2);

        Assert.AreNotEqual(key1, key2);
        Assert.StartsWith("MicHelperServer_", key1);
        Assert.StartsWith("MicHelperServer_", key2);
    }

    [TestMethod]
    public void SetStartupEnabled_AddAndRemove_WorksCorrectly()
    {
        var testKey = @"Software\MicHelperTest_Run_" + Guid.NewGuid().ToString("N");
        try
        {
            var exe = @"C:\MicApp\MicHelper.Server.exe";

            // Initially disabled
            Assert.IsFalse(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exe, testKey));

            // Enable
            StartupRegistryManager.SetStartupEnabled("MicHelperServer", true, exe, testKey);
            Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exe, testKey));

            // Disable
            StartupRegistryManager.SetStartupEnabled("MicHelperServer", false, exe, testKey);
            Assert.IsFalse(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exe, testKey));
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(testKey, throwOnMissingSubKey: false); } catch { }
        }
    }

    [TestMethod]
    public void SetStartupEnabled_MultipleFolders_AreIndependent()
    {
        var testKey = @"Software\MicHelperTest_Run_" + Guid.NewGuid().ToString("N");
        try
        {
            var exeFolder1 = @"C:\Folder1\MicHelper.Server.exe";
            var exeFolder2 = @"C:\Folder2\MicHelper.Server.exe";

            // Enable both
            StartupRegistryManager.SetStartupEnabled("MicHelperServer", true, exeFolder1, testKey);
            StartupRegistryManager.SetStartupEnabled("MicHelperServer", true, exeFolder2, testKey);

            Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeFolder1, testKey));
            Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeFolder2, testKey));

            // Disable folder 1 only
            StartupRegistryManager.SetStartupEnabled("MicHelperServer", false, exeFolder1, testKey);

            Assert.IsFalse(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeFolder1, testKey));
            Assert.IsTrue(StartupRegistryManager.IsStartupEnabled("MicHelperServer", exeFolder2, testKey));
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(testKey, throwOnMissingSubKey: false); } catch { }
        }
    }
}
