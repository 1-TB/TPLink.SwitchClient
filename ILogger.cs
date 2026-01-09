namespace TPLink.SwitchClient;

/// <summary>
/// Optional logging interface for debugging switch communication
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Log a message
    /// </summary>
    /// <param name="message">The message to log</param>
    void Log(string message);
}
