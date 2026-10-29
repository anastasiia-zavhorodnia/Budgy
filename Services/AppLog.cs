using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Budgy.Services;

/// <summary>Статичний доступ до логера для місць, де неможливо отримати ILogger через DI (ViewModelBase, глобальні обробники).</summary>
public static class AppLog
{
    private static ILogger _logger = NullLogger.Instance;

    public static string LogDirectory => Path.Combine(FileSystem.AppDataDirectory, "logs");

    public static void Init(ILoggerFactory factory) => _logger = factory.CreateLogger("Budgy");

    public static void Error(Exception ex, string context) => _logger.LogError(ex, "{Context}", context);
    public static void Critical(Exception? ex, string context) => _logger.LogCritical(ex, "{Context}", context);
}
