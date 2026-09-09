using System.Diagnostics;
using System.Text;

namespace MicHelper.Shared.Common;

public static class AppLogger
{
    private static readonly object s_lock = new();
    private static string? s_logFilePath;
    private static bool s_debugEnabled;
    private static bool s_initialized;

    public static string LogFilePath => s_logFilePath ?? string.Empty;

    public static bool IsDebugEnabled
    {
        get { lock (s_lock) return s_debugEnabled; }
        set { lock (s_lock) s_debugEnabled = value; }
    }

    public static void ResetForTesting()
    {
        lock (s_lock)
        {
            s_logFilePath = null;
            s_debugEnabled = false;
            s_initialized = false;
        }
    }

    public static void Initialize(string appName, string? directory = null, bool debugEnabled = false)
    {
        lock (s_lock)
        {
            var baseDir = directory ?? AppDomain.CurrentDomain.BaseDirectory;
            s_logFilePath = Path.Combine(baseDir, $"{appName.ToLowerInvariant()}.log");
            s_debugEnabled = debugEnabled;

            if (!s_initialized)
            {
                s_initialized = true;

                // Check for log file rotation if > 5 MB
                try
                {
                    if (File.Exists(s_logFilePath))
                    {
                        var info = new FileInfo(s_logFilePath);
                        if (info.Length > 5 * 1024 * 1024)
                        {
                            var oldPath = Path.Combine(baseDir, $"{appName.ToLowerInvariant()}.old.log");
                            File.Move(s_logFilePath, oldPath, overwrite: true);
                        }
                    }
                }
                catch
                {
                }

                Info($"=== {appName} session started (PID={Environment.ProcessId}, OS={Environment.OSVersion}, .NET={Environment.Version}) ===");
            }
        }
    }

    public static void Debug(string message)
    {
        lock (s_lock)
        {
            if (!s_debugEnabled) return;
        }
        WriteEntry("DEBUG", message);
    }

    public static void Info(string message)
    {
        WriteEntry("INFO", message);
    }

    public static void Warn(string message)
    {
        WriteEntry("WARN", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        var sb = new StringBuilder(message);
        if (ex != null)
        {
            sb.AppendLine();
            sb.Append(FormatFullException(ex));
        }
        WriteEntry("ERROR", sb.ToString());
    }

    public static void LogUnhandledException(string source, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"!!! FATAL / UNHANDLED EXCEPTION [{source}] !!!");
        if (ex != null)
        {
            sb.Append(FormatFullException(ex));
        }
        else
        {
            sb.Append("No exception object available.");
        }
        WriteEntry("FATAL", sb.ToString());
    }

    private static string FormatFullException(Exception ex)
    {
        var sb = new StringBuilder();
        Exception? cur = ex;
        int level = 0;
        while (cur != null)
        {
            if (level > 0) sb.AppendLine($"--- Inner Exception [{level}] ---");
            sb.AppendLine($"{cur.GetType().FullName}: {cur.Message}");
            if (cur.StackTrace != null)
            {
                sb.AppendLine(cur.StackTrace);
            }
            cur = cur.InnerException;
            level++;
        }
        return sb.ToString().TrimEnd();
    }

    private static void WriteEntry(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string threadId = Environment.CurrentManagedThreadId.ToString().PadLeft(2);
        string line = $"[{timestamp}] [T{threadId}] [{level,-5}] {message}";

        System.Diagnostics.Debug.WriteLine(line);

        lock (s_lock)
        {
            if (string.IsNullOrEmpty(s_logFilePath)) return;

            try
            {
                var dir = Path.GetDirectoryName(s_logFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(s_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
