using System.Diagnostics;

namespace brightnessControl.Services;

public sealed class AppLogger
{
    public event EventHandler<string>? MessageLogged;

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warning(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {level} {message}";
        Debug.WriteLine(line);
        MessageLogged?.Invoke(this, line);
    }
}
