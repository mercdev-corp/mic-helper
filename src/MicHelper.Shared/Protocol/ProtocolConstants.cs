namespace MicHelper.Shared.Protocol;

public static class ProtocolConstants
{
    public const string DefaultMulticastAddress = "239.255.132.5";
    public const int DefaultPort = 13205;
    public const int DefaultRetryTimeoutSeconds = 5;
    public const int ProtocolVersion = 1;
    public const uint MagicHeader = 0x4843494D; // "MICH" in little-endian (0x4D, 0x49, 0x43, 0x48)
    
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1.0);
    public const int BurstCount = 3;
    public static readonly TimeSpan BurstDelay = TimeSpan.FromMilliseconds(15);
}
