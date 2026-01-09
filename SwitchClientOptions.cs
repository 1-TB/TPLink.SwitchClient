namespace TPLink.SwitchClient;

/// <summary>
/// Configuration options for connecting to a TPLink switch
/// </summary>
public class SwitchClientOptions
{
    /// <summary>
    /// Switch web interface address (including protocol, e.g., http://192.168.1.5)
    /// </summary>
    public required string SwitchWebAddress { get; set; }

    /// <summary>
    /// Switch admin username
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Switch admin password
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Optional logger for debugging HTTP requests/responses
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// HTTP client timeout (default: 20 seconds)
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
}
