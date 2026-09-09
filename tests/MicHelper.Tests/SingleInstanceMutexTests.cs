using MicHelper.Shared.Common;

namespace MicHelper.Tests;

[TestClass]
public sealed class SingleInstanceMutexTests
{
    [TestMethod]
    public void ComputePathHash_DeterministicAndCaseInsensitive()
    {
        var path1 = @"C:\Games\MicHelper";
        var path2 = @"c:\games\michelper\";

        var hash1 = SingleInstanceMutex.ComputePathHash(path1);
        var hash2 = SingleInstanceMutex.ComputePathHash(path2);

        Assert.AreEqual(hash1, hash2);
        Assert.AreEqual(16, hash1.Length);
    }

    [TestMethod]
    public void TryAcquire_SameFolder_BlocksSecondInstance()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "MicHelperTest1_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            using var instance1 = SingleInstanceMutex.TryAcquire("MicHelperServer", tempFolder);
            Assert.IsNotNull(instance1);
            Assert.IsTrue(instance1.IsAcquired);

            // Mutex ownership is thread-affine in Windows; verify a different thread cannot acquire it
            SingleInstanceMutex? instance2 = null;
            var thread = new Thread(() =>
            {
                instance2 = SingleInstanceMutex.TryAcquire("MicHelperServer", tempFolder);
            });
            thread.Start();
            thread.Join();

            using (instance2)
            {
                Assert.IsNull(instance2);
            }
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }

    [TestMethod]
    public void TryAcquire_DifferentFolders_AllowsConcurrentInstances()
    {
        var tempFolder1 = Path.Combine(Path.GetTempPath(), "MicHelperTest2A_" + Guid.NewGuid().ToString("N"));
        var tempFolder2 = Path.Combine(Path.GetTempPath(), "MicHelperTest2B_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder1);
        Directory.CreateDirectory(tempFolder2);

        try
        {
            using var instance1 = SingleInstanceMutex.TryAcquire("MicHelperServer", tempFolder1);
            Assert.IsNotNull(instance1);

            using var instance2 = SingleInstanceMutex.TryAcquire("MicHelperServer", tempFolder2);
            Assert.IsNotNull(instance2);
        }
        finally
        {
            if (Directory.Exists(tempFolder1)) Directory.Delete(tempFolder1, true);
            if (Directory.Exists(tempFolder2)) Directory.Delete(tempFolder2, true);
        }
    }

    [TestMethod]
    public void TryAcquire_AfterDispose_AllowsReacquisition()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "MicHelperTest3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var instance1 = SingleInstanceMutex.TryAcquire("MicHelperClient", tempFolder);
            Assert.IsNotNull(instance1);
            instance1.Dispose();

            using var instance2 = SingleInstanceMutex.TryAcquire("MicHelperClient", tempFolder);
            Assert.IsNotNull(instance2);
        }
        finally
        {
            if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
        }
    }
}
