using System.Reflection;
using System.Text.Json;

namespace ShellyConnector;

public class VersionInfo
{
    public string Version { get; set; } = "0.0.0";
    public string BuildDate { get; set; } = "unknown";
    public string GitCommit { get; set; } = "unknown";
    public string BuildNumber { get; set; } = "0";

    public static VersionInfo GetVersionInfo()
    {
        try
        {
            // Try to read from version.json file first (when running in Docker)
            var versionFilePath = Path.Combine(AppContext.BaseDirectory, "version.json");
            if (File.Exists(versionFilePath))
            {
                var json = File.ReadAllText(versionFilePath);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (versionInfo != null)
                {
                    return versionInfo;
                }
            }
        }
        catch
        {
            // Fall through to assembly version
        }

        // Fallback to assembly version (for local development)
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
        
        return new VersionInfo
        {
            Version = version,
            BuildDate = "development",
            GitCommit = informationalVersion.Contains('+') ? informationalVersion.Split('+')[1] : "dev",
            BuildNumber = "dev"
        };
    }

    public string GetDisplayString()
    {
        return $"Version: {Version} | Build: {BuildNumber} | Commit: {GitCommit} | Date: {BuildDate}";
    }

    public override string ToString()
    {
        return GetDisplayString();
    }
}
