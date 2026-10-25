using AbsoluteBot.Events;
using AbsoluteBot.Services;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat;

/// <summary>
///     Основной класс чат-бота, который управляет командными службами и взаимодействует с сервисами чатов.
/// </summary>
public class ChatBot
{
    private readonly CommandExecutionService _commandExecutionService;
    private readonly IEnumerable<IAsyncInitializable> _initializableServices;
    private readonly IEnumerable<IChatService> _chatServices;

    public ChatBot(CommandExecutionService commandExecutionService, IEnumerable<IChatService> chatServices,
        IEnumerable<IAsyncInitializable> initializableServices)
    {
        _commandExecutionService = commandExecutionService;
        _chatServices = chatServices;
        _initializableServices = initializableServices;
        // Подписка на событие получения сообщений для каждого сервиса чатов
        foreach (var chatService in _chatServices) chatService.MessageReceived += OnMessageReceived;
    }

    /// <summary>
    ///     Запускает чат-бот, инициируя подключение ко всем сервисам чатов.
    /// </summary>
    public async Task Start()
    {
        // Инициализация конфигурационного сервиса в первую очередь
        var configService = _initializableServices.OfType<ConfigService>().FirstOrDefault();
        if (configService != null) await configService.InitializeAsync().ConfigureAwait(false);

        // Инициализация остальных сервисов, за исключением ConfigService
        foreach (var service in _initializableServices)
            if (service != configService)
                await service.InitializeAsync().ConfigureAwait(false);

        // Подключение всех сервисов чатов
        foreach (var chatService in _chatServices) await chatService.Connect().ConfigureAwait(false);
    }

    /// <summary>
    ///     Обрабатывает событие получения сообщения и передает текст на выполнение команд.
    /// </summary>
    private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        await _commandExecutionService.ExecuteCommandAsync(e.Text, e.Context).ConfigureAwait(false);
    }
}