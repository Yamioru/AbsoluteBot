using AbsoluteBot.Chat;
using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Services.ScheduledTasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using System.Text.Json;

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

            // Запуск HTTP-сервера, чтобы приложение отвечало по домену VDS
            var webApplication = BuildWebApplication();
            await webApplication.StartAsync().ConfigureAwait(false);

            // Запуск чат-бота и инициализация сервисов
            await StartChatBot(serviceProvider).ConfigureAwait(false);

            // Регистрация команд
            RegisterCommands(serviceProvider);

            // Запуск периодических задач
            StartScheduledTasks(serviceProvider);

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

    private static WebApplication BuildWebApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:5000");

        var app = builder.Build();

        app.MapPost("/Sobeka", async (HttpContext context) =>
        {
            // 1. Читаем JSON от Яндекса
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            // Используем JsonDocument для простоты, чтобы не создавать много классов
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Достаем текст, который сказал пользователь
            var command = root.GetProperty("request").GetProperty("command").GetString() ?? "";
            var normalizedText = command.Trim().ToLowerInvariant();

            // 2. Логика ответа
            string replyText = "Я не знаю, что ответить";
            if (normalizedText.Contains("привет"))
            {
                replyText = "Пока!";
            }
            else if (string.IsNullOrEmpty(normalizedText))
            {
                replyText = "Привет! Я слушаю. Скажи мне что-нибудь.";
            }

            // 3. Формируем ответ строго по протоколу Яндекса
            var responseJson = new
            {
                response = new
                {
                    text = replyText,
                    tts = replyText, // Текст для озвучки (можно добавить паузы или ударения)
                    end_session = false // Если true - Алиса закроет навык после этой фразы
                },
                version = "1.0"
            };

            // Возвращаем JSON
            return Results.Json(responseJson);
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
    ///     Настраивает все необходимые службы и возвращает ServiceProvider.
    /// </summary>
    /// <returns>Поставщик служб с зарегистрированными зависимостями.</returns>
    private static ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true)) // Настройка логирования через Serilog
            .ConfigureHttpClients() // Конфигурация Http-клиентов
            .RegisterServices() // Регистрация сервисов
            .RegisterCommands() // Регистрация команд
            .BuildServiceProvider(); // Построение поставщика служб
    }

    /// <summary>
    ///     Регистрация команд, реализующих интерфейс IChatCommand.
    /// </summary>
    /// <param name="serviceProvider">Поставщик служб.</param>
    private static void RegisterCommands(ServiceProvider serviceProvider)
    {
        var commandRegistry = serviceProvider.GetService<ICommandRegistry>();
        var commands = serviceProvider.GetServices<IChatCommand>();
        foreach (var command in commands) commandRegistry?.RegisterCommand(command);
    }

    /// <summary>
    ///     Настраивает обработчики завершения приложения, такие как Ctrl+C или завершение процесса.
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
    ///     Запускает чат-бот.
    /// </summary>
    /// <param name="serviceProvider">Поставщик служб.</param>
    private static async Task StartChatBot(ServiceProvider serviceProvider)
    {
        var chatBot = serviceProvider.GetService<ChatBot>();
        if (chatBot == null) return;
        await chatBot.Start().ConfigureAwait(false);
    }

    /// <summary>
    ///     Запускает периодические задачи.
    /// </summary>
    /// <param name="serviceProvider">Поставщик служб.</param>
    private static void StartScheduledTasks(ServiceProvider serviceProvider)
    {
        var scheduledTaskService = serviceProvider.GetService<ScheduledTaskService>();
        scheduledTaskService?.Start();
    }

    /// <summary>
    ///     Ожидает сигнала завершения работы приложения.
    /// </summary>
    private static async Task WaitForShutdownSignalAsync()
    {
        await ShutdownCompletionSource.Task.ConfigureAwait(false);
    }

    private static async Task<string> ExtractRequestTextAsync(HttpRequest request)
    {
        if (request.Query.TryGetValue("text", out var queryText) && !string.IsNullOrWhiteSpace(queryText))
            return queryText.ToString();

        var pathText = request.Path.Value?.Trim('/');
        if (!string.IsNullOrWhiteSpace(pathText))
            return pathText;

        if (request.ContentLength is > 0)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync().ConfigureAwait(false);
            request.Body.Position = 0;
            if (!string.IsNullOrWhiteSpace(bodyText))
                return bodyText;
        }

        return string.Empty;
    }
}