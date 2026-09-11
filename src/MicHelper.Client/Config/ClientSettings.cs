using System.Text.Json;
using System.Text.Json.Serialization;
using MicHelper.Shared.Protocol;

namespace MicHelper.Client.Config;

[JsonConverter(typeof(JsonStringEnumConverter<ClientMode>))]
public enum ClientMode
{
    DualPc,
    SinglePc
}

[JsonSerializable(typeof(ClientSettings))]
[JsonSerializable(typeof(ClientMode))]
internal partial class ClientSettingsJsonContext : JsonSerializerContext
{
}

public sealed class ClientSettings
{
    public const string SettingsFileName = "mic-helper-client-settings.json";

    public ClientMode Mode { get; set; } = ClientMode.DualPc;
    public string? MicrophoneId { get; set; }
    public string? MicrophoneName { get; set; }
    public bool RunOnStartup { get; set; }
    public int Port { get; set; } = ProtocolConstants.DefaultPort;
    public string? ServerIp { get; set; }
    public int RetryTimeout { get; set; } = ProtocolConstants.DefaultRetryTimeoutSeconds;
    public int Opacity { get; set; } = 100; // 0 to 100
    public double PulseFrequency { get; set; } = 1.0; // 0.1 to 5.0 seconds
    public int OverlayCenterX { get; set; } = 200;
    public int OverlayCenterY { get; set; } = 200;
    public int OverlayWidth { get; set; } = 128;
    public int OverlayHeight { get; set; } = 128;
    public bool IsPaused { get; set; }
    public bool DebugLogging { get; set; }

    [JsonIgnore]
    public string? SettingsDirectory { get; set; }

    public static string GetFilePath(string? directory = null)
    {
        var baseDir = directory ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, SettingsFileName);
    }

    public static ClientSettings Load(string? directory = null)
    {
        var path = GetFilePath(directory);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize(json, ClientSettingsJsonContext.Default.ClientSettings);
                if (settings != null)
                {
                    settings.SettingsDirectory = directory;
                    if (settings.Port is < 1 or > 65535) settings.Port = ProtocolConstants.DefaultPort;
                    if (settings.RetryTimeout < 1) settings.RetryTimeout = ProtocolConstants.DefaultRetryTimeoutSeconds;
                    settings.Opacity = Math.Clamp(settings.Opacity, 0, 100);
                    settings.PulseFrequency = Math.Clamp(settings.PulseFrequency, 0.1, 5.0);
                    settings.OverlayWidth = Math.Clamp(settings.OverlayWidth, 32, 1024);
                    settings.OverlayHeight = Math.Clamp(settings.OverlayHeight, 32, 1024);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load client settings: {ex.Message}");
            }
        }

        return new ClientSettings { SettingsDirectory = directory };
    }

    public void Save(string? directory = null)
    {
        if (!string.IsNullOrEmpty(directory))
        {
            SettingsDirectory = directory;
        }

        var path = GetFilePath(SettingsDirectory);
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, ClientSettingsJsonContext.Default.ClientSettings);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save client settings: {ex.Message}");
        }
    }
}
