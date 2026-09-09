using MicHelper.Shared.Common;

namespace MicHelper.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AppLoggerTests
{
    private static readonly object s_testLock = new();

    [TestCleanup]
    public void Cleanup()
    {
        AppLogger.ResetForTesting();
    }

    [TestMethod]
    public void AppLogger_WritesEntries_WithCorrectLevelsAndTimestamps()
    {
        lock (s_testLock)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "AppLoggerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                AppLogger.ResetForTesting();
                AppLogger.Initialize("test-app", tempFolder, debugEnabled: false);

                var logFile = AppLogger.LogFilePath;
                Assert.IsTrue(File.Exists(logFile), "Log file should be created");

                AppLogger.Info("Informational test message");
                AppLogger.Warn("Warning test message");
                AppLogger.Error("Error test message", new InvalidOperationException("Inner test boom"));

                // Debug shouldn't be written when debugEnabled is false
                AppLogger.Debug("Debug test message (should not appear)");

                var content = File.ReadAllText(logFile);
                StringAssert.Contains(content, "session started");
                StringAssert.Contains(content, "[INFO ] Informational test message");
                StringAssert.Contains(content, "[WARN ] Warning test message");
                StringAssert.Contains(content, "[ERROR] Error test message");
                StringAssert.Contains(content, "InvalidOperationException: Inner test boom");
                Assert.DoesNotContain("Debug test message (should not appear)", content);

                // Now enable debug logging and verify it gets recorded
                AppLogger.IsDebugEnabled = true;
                AppLogger.Debug("Debug test message (now enabled)");

                content = File.ReadAllText(logFile);
                StringAssert.Contains(content, "[DEBUG] Debug test message (now enabled)");
            }
            finally
            {
                AppLogger.ResetForTesting();
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }
    }

    [TestMethod]
    public void AppLogger_LogUnhandledException_FormatsExceptionDetails()
    {
        lock (s_testLock)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "AppLoggerCrashTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                AppLogger.ResetForTesting();
                AppLogger.Initialize("crash-test", tempFolder, debugEnabled: false);

                var logFile = AppLogger.LogFilePath;

                try
                {
                    throw new ArgumentException("Parameter is invalid", new NullReferenceException("Root cause null ref"));
                }
                catch (Exception ex)
                {
                    AppLogger.LogUnhandledException("TestCrashSource", ex);
                }

                var content = File.ReadAllText(logFile);
                StringAssert.Contains(content, "FATAL");
                StringAssert.Contains(content, "!!! FATAL / UNHANDLED EXCEPTION [TestCrashSource] !!!");
                StringAssert.Contains(content, "ArgumentException: Parameter is invalid");
                StringAssert.Contains(content, "NullReferenceException: Root cause null ref");
            }
            finally
            {
                AppLogger.ResetForTesting();
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }
    }

    [TestMethod]
    public void AppLogger_RotatesFile_WhenSizeExceeds5MB()
    {
        lock (s_testLock)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "AppLoggerRotateTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                AppLogger.ResetForTesting();
                var logPath = Path.Combine(tempFolder, "rotate-app.log");
                var oldPath = Path.Combine(tempFolder, "rotate-app.old.log");

                // Pre-create a file with > 5MB size
                using (var fs = new FileStream(logPath, FileMode.Create, FileAccess.Write))
                {
                    fs.SetLength(5 * 1024 * 1024 + 1024); // 5MB + 1KB
                }

                Assert.IsTrue(File.Exists(logPath));
                Assert.IsFalse(File.Exists(oldPath));

                // Initialize should detect > 5MB and move to .old.log
                AppLogger.Initialize("rotate-app", tempFolder, debugEnabled: false);

                Assert.IsTrue(File.Exists(oldPath), "Old rotated log should exist");
                Assert.IsTrue(File.Exists(logPath), "New active log file should have been created");

                var newContent = File.ReadAllText(logPath);
                StringAssert.Contains(newContent, "session started");
            }
            finally
            {
                AppLogger.ResetForTesting();
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }
    }
}
