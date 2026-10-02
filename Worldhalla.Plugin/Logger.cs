namespace Worldhalla.Plugin;

public static class Logger
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
    }

    private static Action<LogLevel, string>? impl;

    public static void SetLogger(Action<LogLevel, string> logger)
    {
        impl = logger;
    }

    public static void LogInfo(string message)
    {
        impl?.Invoke(LogLevel.Info, message);
    }

    public static void LogWarning(string message)
    {
        impl?.Invoke(LogLevel.Warning, message);
    }

    public static void LogError(string message)
    {
        impl?.Invoke(LogLevel.Error, message);
    }
}
