using Microsoft.Extensions.Logging;

namespace Budgy.Services;

/// <summary>Простий файловий логер: AppDataDirectory/logs/budgy-yyyyMMdd.log (рівень Information і вище).</summary>
public sealed class FileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly object _sync = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, directory, _sync);

    public void Dispose() { }

    private sealed class FileLogger(string category, string directory, object sync) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;

            try
            {
                lock (sync)
                {
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(Path.Combine(directory, $"budgy-{DateTime.Now:yyyyMMdd}.log"),
                        line + Environment.NewLine);
                }
            }
            catch
            {
                // логер не повинен ламати додаток
            }
        }
    }
}
