using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MicHelper.Shared.Protocol;

namespace MicHelper.Shared.Network;

public sealed record DiscoveredServer(
    string ServerId,
    string HostName,
    string ServerIp,
    string MicrophoneName,
    MicState LastState,
    DateTime LastSeen);

public sealed class UdpListener : IDisposable
{
    private readonly object _lock = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private System.Threading.Timer? _watchdogTimer;

    private int _port;
    private string? _targetServerIp;
    private int _retryTimeoutSeconds = 5;
    private bool _isPaused;
    private bool _isConnected;
    private MicState _lastReportedState = MicState.Disconnected;
    private DateTime _lastTargetPacketTime = DateTime.MinValue;
    private bool _disposed;

    private readonly ConcurrentDictionary<string, DiscoveredServer> _discoveredServers = new();

    public int Port => _port;
    public string? TargetServerIp => _targetServerIp;
    public bool IsConnected => _isConnected;
    public MicState LastReportedState => _lastReportedState;
    public bool IsPaused => _isPaused;

    public event Action<StatusPacket, IPEndPoint>? PacketReceived;
    public event Action<MicState>? TargetServerStateChanged;
    public event Action<bool>? TargetServerConnectionChanged;
    public event Action? DiscoveredServersUpdated;

    public UdpListener(int port = ProtocolConstants.DefaultPort)
    {
        _port = port;
    }

    public IReadOnlyList<DiscoveredServer> GetDiscoveredServers()
    {
        // Prune servers not seen in last 60 seconds
        var threshold = DateTime.UtcNow.AddSeconds(-60);
        foreach (var kvp in _discoveredServers)
        {
            if (kvp.Value.LastSeen < threshold)
            {
                _discoveredServers.TryRemove(kvp.Key, out _);
            }
        }
        return _discoveredServers.Values.OrderByDescending(s => s.LastSeen).ToList();
    }

    public void Start(string? targetServerIp = null, int retryTimeoutSeconds = 5)
    {
        lock (_lock)
        {
            _targetServerIp = targetServerIp;
            _retryTimeoutSeconds = Math.Max(1, retryTimeoutSeconds);
            _isPaused = false;
            _lastTargetPacketTime = DateTime.MinValue;
        }

        SetConnected(false);
        SetState(MicState.Disconnected);

        RestartSocket();
        StartWatchdog();
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isPaused = true;
            StopWatchdog();
            CloseSocket();
        }

        SetConnected(false);
        SetState(MicState.Disconnected);
    }

    public void SetPaused(bool paused)
    {
        lock (_lock)
        {
            if (_isPaused == paused) return;
            _isPaused = paused;
        }

        if (paused)
        {
            Stop();
            SetConnected(false);
        }
        else
        {
            Start(_targetServerIp, _retryTimeoutSeconds);
        }
    }

    public void Rebind(int newPort, string? newTargetServerIp = null)
    {
        lock (_lock)
        {
            _port = newPort;
            if (newTargetServerIp != null)
            {
                _targetServerIp = newTargetServerIp;
            }
            _discoveredServers.Clear();
        }

        if (!_isPaused)
        {
            RestartSocket();
        }
    }

    public void SetTargetServer(string? serverIp)
    {
        bool changed = false;
        lock (_lock)
        {
            if (!string.Equals(_targetServerIp, serverIp, StringComparison.OrdinalIgnoreCase))
            {
                _targetServerIp = serverIp;
                _lastTargetPacketTime = DateTime.MinValue;
                changed = true;
            }
        }

        if (changed)
        {
            SetConnected(false);
        }
    }

    public void SetRetryTimeout(int seconds)
    {
        lock (_lock)
        {
            _retryTimeoutSeconds = Math.Max(1, seconds);
        }
    }

    private void RestartSocket()
    {
        lock (_lock)
        {
            CloseSocket();

            try
            {
                _cts = new CancellationTokenSource();
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                _udpClient.JoinMulticastGroup(IPAddress.Parse(ProtocolConstants.DefaultMulticastAddress));
                _udpClient.MulticastLoopback = true;

                var token = _cts.Token;
                Task.Run(() => ReceiveLoopAsync(_udpClient, token), token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to bind UDP listener on port {_port}: {ex.Message}");
            }
        }
    }

    private void CloseSocket()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _udpClient?.Dispose();
        _udpClient = null;
    }

    private async Task ReceiveLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                ProcessIncomingDatagram(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) break;
                System.Diagnostics.Debug.WriteLine($"UDP Receive error: {ex.Message}");
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void ProcessIncomingDatagram(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        if (!StatusPacket.TryParse(buffer, out var packet))
        {
            return;
        }

        var senderIp = remoteEndPoint.Address.ToString();
        var serverInfo = new DiscoveredServer(
            packet.ServerId,
            packet.HostName,
            senderIp,
            packet.MicrophoneName,
            packet.State,
            DateTime.UtcNow);

        bool isNewOrChanged = false;
        if (!_discoveredServers.TryGetValue(senderIp, out var existing))
        {
            isNewOrChanged = true;
        }
        else if (existing.HostName != serverInfo.HostName ||
                 existing.MicrophoneName != serverInfo.MicrophoneName ||
                 existing.ServerId != serverInfo.ServerId ||
                 (DateTime.UtcNow - existing.LastSeen) > TimeSpan.FromSeconds(_retryTimeoutSeconds))
        {
            isNewOrChanged = true;
        }

        _discoveredServers[senderIp] = serverInfo;

        if (isNewOrChanged)
        {
            DiscoveredServersUpdated?.Invoke();
        }

        PacketReceived?.Invoke(packet, remoteEndPoint);

        // Check if this packet matches our target server
        bool isTarget = false;
        lock (_lock)
        {
            if (string.IsNullOrEmpty(_targetServerIp))
            {
                // If no specific server target configured, first active server becomes target
                _targetServerIp = senderIp;
                isTarget = true;
            }
            else if (string.Equals(_targetServerIp, senderIp, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_targetServerIp, packet.ServerId, StringComparison.OrdinalIgnoreCase))
            {
                isTarget = true;
            }

            if (isTarget)
            {
                _lastTargetPacketTime = DateTime.UtcNow;
            }
        }

        if (isTarget)
        {
            SetConnected(true);
            SetState(packet.State);
        }
    }

    private void SetConnected(bool connected)
    {
        bool changed = false;
        lock (_lock)
        {
            if (_isConnected != connected)
            {
                _isConnected = connected;
                changed = true;
            }
        }

        if (changed)
        {
            TargetServerConnectionChanged?.Invoke(connected);
        }
    }

    private void SetState(MicState state)
    {
        bool changed = false;
        lock (_lock)
        {
            if (_lastReportedState != state)
            {
                _lastReportedState = state;
                changed = true;
            }
        }

        if (changed)
        {
            TargetServerStateChanged?.Invoke(state);
        }
    }

    private void StartWatchdog()
    {
        lock (_lock)
        {
            _watchdogTimer?.Dispose();
            _watchdogTimer = new System.Threading.Timer(OnWatchdogTick, null, 1000, 1000);
        }
    }

    private void StopWatchdog()
    {
        lock (_lock)
        {
            _watchdogTimer?.Dispose();
            _watchdogTimer = null;
        }
    }

    private void OnWatchdogTick(object? state)
    {
        if (_disposed || _isPaused) return;

        bool timedOut = false;
        lock (_lock)
        {
            if (_isConnected)
            {
                var elapsed = DateTime.UtcNow - _lastTargetPacketTime;
                if (elapsed > TimeSpan.FromSeconds(_retryTimeoutSeconds))
                {
                    timedOut = true;
                }
            }
        }

        if (timedOut)
        {
            SetConnected(false);
            SetState(MicState.Disconnected);
            DiscoveredServersUpdated?.Invoke();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        StopWatchdog();
        CloseSocket();
    }
}
