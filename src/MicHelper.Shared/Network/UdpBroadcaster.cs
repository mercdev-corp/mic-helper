using System.Net;
using System.Net.Sockets;
using MicHelper.Shared.Protocol;

namespace MicHelper.Shared.Network;

public sealed class UdpBroadcaster : IDisposable
{
    private readonly object _lock = new();
    private UdpClient? _udpClient;
    private IPEndPoint _multicastEndpoint;
    private System.Threading.Timer? _heartbeatTimer;

    private int _port;
    private ulong _sequenceNumber;
    private MicState? _currentState;
    private bool _isPaused;
    private bool _disposed;

    public string ServerId { get; }
    public string HostName { get; }
    public string MicrophoneName { get; set; } = string.Empty;
    public int Port => _port;
    public MicState CurrentState => _currentState ?? MicState.Unmuted;
    public bool IsPaused => _isPaused;

    public event Action<StatusPacket>? PacketSent;

    public UdpBroadcaster(int port = ProtocolConstants.DefaultPort, string? serverId = null)
    {
        _port = port;
        ServerId = serverId ?? Guid.NewGuid().ToString("N");
        HostName = NetworkUtils.GetLocalHostName();
        _multicastEndpoint = new IPEndPoint(IPAddress.Parse(ProtocolConstants.DefaultMulticastAddress), _port);

        InitializeSocket();
    }

    private void InitializeSocket()
    {
        lock (_lock)
        {
            _udpClient?.Dispose();

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0)); // ephemeral source port
            _udpClient.JoinMulticastGroup(IPAddress.Parse(ProtocolConstants.DefaultMulticastAddress));
            _udpClient.MulticastLoopback = true;
        }
    }

    public void Rebind(int newPort)
    {
        lock (_lock)
        {
            if (_port == newPort) return;
            _port = newPort;
            _multicastEndpoint = new IPEndPoint(IPAddress.Parse(ProtocolConstants.DefaultMulticastAddress), _port);
            InitializeSocket();
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new System.Threading.Timer(
                _ => SendHeartbeat(),
                null,
                TimeSpan.Zero,
                ProtocolConstants.HeartbeatInterval);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }
    }

    public void SetPaused(bool paused)
    {
        bool transition = false;
        lock (_lock)
        {
            if (_isPaused != paused)
            {
                _isPaused = paused;
                if (paused)
                {
                    _currentState = MicState.Paused;
                }
                transition = true;
            }
        }

        if (transition)
        {
            if (paused)
            {
                // Explicitly announce Paused before pausing heartbeats
                BroadcastImmediateBurst(MicState.Paused, PacketType.StateChange);
                Stop();
            }
            else
            {
                Start();
                BroadcastImmediateBurst(CurrentState, PacketType.StateChange);
            }
        }
    }

    public void UpdateState(MicState newState, string? micName = null, bool force = false)
    {
        bool changed = false;
        lock (_lock)
        {
            if (micName != null) MicrophoneName = micName;

            if (!_isPaused && (force || _currentState != newState))
            {
                _currentState = newState;
                changed = true;
            }
        }

        if (changed)
        {
            BroadcastImmediateBurst(newState, PacketType.StateChange);
        }
    }

    private void BroadcastImmediateBurst(MicState state, PacketType type)
    {
        Task.Run(async () =>
        {
            for (int i = 0; i < ProtocolConstants.BurstCount; i++)
            {
                if (_disposed) break;
                SendPacket(state, type);
                if (i < ProtocolConstants.BurstCount - 1)
                {
                    await Task.Delay(ProtocolConstants.BurstDelay).ConfigureAwait(false);
                }
            }
        });
    }

    private void SendHeartbeat()
    {
        MicState state;
        bool paused;
        lock (_lock)
        {
            state = CurrentState;
            paused = _isPaused;
        }

        if (paused || _disposed) return;
        SendPacket(state, PacketType.Heartbeat);
    }

    private void SendPacket(MicState state, PacketType type)
    {
        byte[] bytes;
        StatusPacket packet;

        lock (_lock)
        {
            if (_disposed || _udpClient == null) return;

            _sequenceNumber++;
            packet = new StatusPacket
            {
                Type = type,
                State = state,
                SequenceNumber = _sequenceNumber,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ServerId = ServerId,
                HostName = HostName,
                MicrophoneName = MicrophoneName
            };

            bytes = packet.ToBytes();
        }

        try
        {
            _udpClient.Send(bytes, bytes.Length, _multicastEndpoint);
            PacketSent?.Invoke(packet);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send UDP packet: {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            _udpClient?.Dispose();
            _udpClient = null;
        }
    }
}
