using System.Security.Cryptography;
using System.Text;

namespace MicHelper.Shared.Common;

public sealed class SingleInstanceMutex : IDisposable
{
    private Mutex? _mutex;
    private bool _hasHandle;
    private bool _disposed;

    public string MutexName { get; }
    public string PathHash { get; }
    public bool IsAcquired => _hasHandle;

    private SingleInstanceMutex(Mutex mutex, bool hasHandle, string mutexName, string pathHash)
    {
        _mutex = mutex;
        _hasHandle = hasHandle;
        MutexName = mutexName;
        PathHash = pathHash;
    }

    public static string ComputePathHash(string path)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedPath);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes)[..16];
    }

    public static string GenerateMutexName(string appPrefix, string? directory = null)
    {
        var baseDir = directory ?? AppDomain.CurrentDomain.BaseDirectory;
        var hash = ComputePathHash(baseDir);
        return $"{appPrefix}_{hash}";
    }

    public static SingleInstanceMutex? TryAcquire(string appPrefix, string? directory = null)
    {
        var baseDir = directory ?? AppDomain.CurrentDomain.BaseDirectory;
        var hash = ComputePathHash(baseDir);
        var mutexName = $"{appPrefix}_{hash}";

        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: true, name: mutexName, out bool createdNew);
            if (!createdNew)
            {
                if (!mutex.WaitOne(0, false))
                {
                    mutex.Dispose();
                    return null;
                }
            }

            return new SingleInstanceMutex(mutex, true, mutexName, hash);
        }
        catch (AbandonedMutexException)
        {
            if (mutex != null)
            {
                return new SingleInstanceMutex(mutex, true, mutexName, hash);
            }
            return null;
        }
        catch
        {
            mutex?.Dispose();
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hasHandle && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
            _hasHandle = false;
        }

        _mutex?.Dispose();
        _mutex = null;
    }
}
