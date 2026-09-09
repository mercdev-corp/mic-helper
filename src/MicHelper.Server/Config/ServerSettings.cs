using System.Text.Json;
using System.Text.Json.Serialization;
using MicHelper.Shared.Protocol;

namespace MicHelper.Server.Config;

[JsonSerializable(typeof(ServerSettings))]
internal partial class ServerSettingsJsonContext : JsonSerializerContext
{
}

public sealed class ServerSettings
{
    public const string SettingsFileName = "mic-helper-server-settings.json";

    public bool RunOnStartup { get; set; }
    public string? MicrophoneId { get; set; }
    public string? MicrophoneName { get; set; }
    public int Port { get; set; } = ProtocolConstants.DefaultPort;
    public int RetryTimeout { get; set; } = ProtocolConstants.DefaultRetryTimeoutSeconds;
    public bool IsPaused { get; set; }
    public bool DebugLogging { get; set; }

    public static string GetFilePath(string? directory = null)
    {
        var baseDir = directory ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, SettingsFileName);
    }

    public static ServerSettings Load(string? directory = null)
    {
        var path = GetFilePath(directory);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize(json, ServerSettingsJsonContext.Default.ServerSettings);
                if (settings != null)
                {
                    if (settings.Port is < 1 or > 65535) settings.Port = ProtocolConstants.DefaultPort;
                    if (settings.RetryTimeout < 1) settings.RetryTimeout = ProtocolConstants.DefaultRetryTimeoutSeconds;
                    return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load server settings: {ex.Message}");
            }
        }

        return new ServerSettings();
    }

    public void Save(string? directory = null)
    {
        var path = GetFilePath(directory);
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, ServerSettingsJsonContext.Default.ServerSettings);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save server settings: {ex.Message}");
        }
    }
}
