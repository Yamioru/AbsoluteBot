using System.Text.Json;
using AbsoluteBot.Chat;
using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Services.ScheduledTasks;
using AbsoluteBot.Services.TextChat;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace AbsoluteBot;

public class Program
{
    private static readonly TaskCompletionSource<bool> ShutdownCompletionSource = new();

    public static async Task Main()
    {
        // Настройка логирования через Serilog
        ConfigureLogger();

        // Настройка обработчиков для завершения работы
        SetupApplicationShutdown();

        try
        {
            Log.Information("Запуск бота...");

            // Настройка служб
            var serviceProvider = ConfigureServices();

            // Запуск чат-бота и инициализация сервисов
            await StartChatBot(serviceProvider).ConfigureAwait(false);

            // Регистрация команд
            RegisterCommands(serviceProvider);

            // Запуск периодических задач
            StartScheduledTasks(serviceProvider);

            // Запуск HTTP-сервера, чтобы приложение отвечало по домену VDS
            var webApplication = BuildWebApplication(serviceProvider);
            await webApplication.StartAsync().ConfigureAwait(false);

            // Ожидание сигнала завершения
            await WaitForShutdownSignalAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Необработанное исключение возникло при запуске приложения.");
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    private static WebApplication BuildWebApplication(ServiceProvider serviceProvider)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:5000");

        // Не забудьте добавить builder.Services.AddMemoryCache() в Main!
        var app = builder.Build();

        var textCommandService = serviceProvider.GetRequiredService<TextCommandService>();
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();

        app.MapPost("/Sobeka", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // 1. Извлекаем ID сессии, чтобы не путать ответы разных пользователей
            var sessionId = root.GetProperty("session").GetProperty("session_id").GetString() ?? "default";
            var command = root.GetProperty("request").GetProperty("command").GetString() ?? "";
            var normalizedText = command.Trim().ToLowerInvariant();

            // 2. Проверяем, нет ли у нас готового ответа с прошлого «подвисшего» запроса
            if (cache.TryGetValue(sessionId, out string savedResponse))
            {
                cache.Remove(sessionId); // Удаляем, так как сейчас мы его отдадим
                return Results.Json(CreateAliceResponse(savedResponse));
            }

            // 3. Запускаем задачу обработки, но ограничиваем её по времени
            var processTask = textCommandService.ProcessAsync(normalizedText, "Свет");
            var timeoutTask = Task.Delay(2500); // 2.5 секунды

            var completedTask = await Task.WhenAny(processTask, timeoutTask);

            if (completedTask == processTask)
            {
                // Успели вовремя!
                var result = await processTask;
                return Results.Json(CreateAliceResponse(result));
            }

            // Не успели! 
            // Запускаем фоновое продолжение: когда задача доделается, сохраняем в кэш
            _ = processTask.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    // Сохраняем результат на 5 минут (хватит, чтобы пользователь спросил еще раз)
                    cache.Set(sessionId, t.Result, TimeSpan.FromMinutes(5));
            });

            return Results.Json(CreateAliceResponse("Ща, погоди"));
        });

        return app;
    }

    private static void ConfigureLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClientHandler", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpMessageHandler", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogEventLevel.Warning)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(Matching.WithProperty("ConnectionEvent"))
                .WriteTo.File("logs/connection.log", rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}---{NewLine}")
            )
            .WriteTo.Logger(lc => lc
                .Filter.ByExcluding(Matching.WithProperty("ConnectionEvent"))
                .WriteTo.File(
                    "logs/absolute_bot.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}---{NewLine}"
                )
            )
            .WriteTo.Console(LogEventLevel.Information)
            .CreateLogger();
    }

    /// <summary>
    /// Настраивает все необходимые службы и возвращает ServiceProvider.
    /// </summary>
    /// <returns>Поставщик служб с зарегистрированными зависимостями.</returns>
    private static ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddMemoryCache()
            .AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true)) // Настройка логирования через Serilog
            .ConfigureHttpClients() // Конфигурация Http-клиентов
            .RegisterServices() // Регистрация сервисов
            .RegisterCommands() // Регистрация команд
            .BuildServiceProvider(); // Построение поставщика служб
    }

// Вспомогательный метод для формирования структуры ответа
    private static object CreateAliceResponse(string text)
    {
        return new
        {
            response = new
            {
                text,
                tts = text,
                end_session = false
            },
            version = "1.0"
        };
    }

    /// <summary>
    /// Регистрация команд, реализующих интерфейс IChatCommand.
    /// </summary>
    /// <param name="serviceProvider">Поставщик служб.</param>
    private static void RegisterCommands(ServiceProvider serviceProvider)
    {
        var commandRegistry = serviceProvider.GetService<ICommandRegistry>();
        var commands = serviceProvider.GetServices<IChatCommand>();
        foreach (var command in commands) commandRegistry?.RegisterCommand(command);
    }

    /// <summary>
    /// Настраивает обработчики завершения приложения, такие как Ctrl+C или завершение процесса.
    /// </summary>
    private static void SetupApplicationShutdown()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Log.Information("Приложение завершает работу...");
            ShutdownCompletionSource.TrySetResult(true);
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Log.Information("Получен сигнал Ctrl+C. Завершение работы...");
            ShutdownCompletionSource.TrySetResult(true);
        };
    }

    /// <summary>
    /// Запускает чат-бот.
    /// </summary>
    /// <param name="serviceProvider">Поставщик служб.</param>
    private static async Task StartChatBot(ServiceProvider serviceProvider)
    {
        var chatBot = serviceProvider.GetService<ChatBot>();
        if (chatBot == null) return;
        await chatBot.Start().ConfigureAwait(false);
    }

    /// <summary>
    /// Запускает периодические задачи.
    /// </summary>
    /// <param name="serviceProvider">Поставщик служб.</param>
    private static void StartScheduledTasks(ServiceProvider serviceProvider)
    {
        var scheduledTaskService = serviceProvider.GetService<ScheduledTaskService>();
        scheduledTaskService?.Start();
    }

    /// <summary>
    /// Ожидает сигнала завершения работы приложения.
    /// </summary>
    private static async Task WaitForShutdownSignalAsync()
    {
        await ShutdownCompletionSource.Task.ConfigureAwait(false);
    }
}