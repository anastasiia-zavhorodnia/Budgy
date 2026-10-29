using Budgy.Services;
using Microsoft.Extensions.Logging;

namespace Budgy;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // Якщо у вашому проєкті вже є свій блок ConfigureFonts — залиште його як було.
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ---- Логування (лаб. №4): Debug-вікно VS + файл AppDataDirectory/logs/budgy-yyyyMMdd.log ----
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddProvider(new FileLoggerProvider(AppLog.LogDirectory));

        // ---- Сервіси, ViewModel, сторінки ----
        builder.Services.AddFinanceApp();

        var app = builder.Build();

        // ---- Глобальні обробники непередбачених винятків ----
        var factory = app.Services.GetRequiredService<ILoggerFactory>();
        AppLog.Init(factory);
        var logger = factory.CreateLogger("Budgy.Global");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception,
                "Необроблений виняток (IsTerminating={Terminating})", e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Необроблений виняток у фоновому завданні");
            e.SetObserved();
        };

        logger.LogInformation("Budgy запущено");
        return app;
    }
}
