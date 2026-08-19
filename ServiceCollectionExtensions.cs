using AbsoluteBot.Chat;
using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Messaging;
using AbsoluteBot.Services;
using AbsoluteBot.Services.ChatServices;
using AbsoluteBot.Services.ChatServices.Discord;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.ChatServices.TelegramChat;
using AbsoluteBot.Services.ChatServices.TwitchChat;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.ScheduledTasks;
using AbsoluteBot.Services.TextChat;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Microsoft.Extensions.DependencyInjection;

namespace AbsoluteBot;

/// <summary>
///     Статический класс, содержащий методы расширения для регистрации сервисов в <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Настройка HttpClient для сервисов, которые его используют.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными HttpClient сервисами.</returns>
    public static IServiceCollection ConfigureHttpClients(this IServiceCollection services)
    {
        services.AddSingleton(_ => new HttpClient());
        services.AddHttpClient<IGoogleSearchDefinitionService, GoogleSearchDefinitionService>();
        return services;
    }

    /// <summary>
    ///     Регистрация административных команд чата.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с зарегистрированными командами чата.</returns>
    public static IServiceCollection RegisterAdminCommands(this IServiceCollection services)
    {
        return services
            .AddSingleton<IChatCommand, AddCensorWord>()
            .AddSingleton<IChatCommand, AddExtraCommand>()
            .AddSingleton<IChatCommand, AddWisdomCommand>()
            .AddSingleton<IChatCommand, AmnesiaCommand>()
            .AddSingleton<IChatCommand, AutoTranslateCommand>()
            .AddSingleton<IChatCommand, CooldownCommand>()
            .AddSingleton<IChatCommand, DeleteMessagesCommand>()
            .AddSingleton<IChatCommand, IgnoreCommand>()
            .AddSingleton<IChatCommand, ListBirthdaysCommand>()
            .AddSingleton<IChatCommand, RemoveCensorWordCommand>()
            .AddSingleton<IChatCommand, RemoveExtraCommand>()
            .AddSingleton<IChatCommand, RestartCommand>()
            .AddSingleton<IChatCommand, SetConfigValueCommand>()
            .AddSingleton<IChatCommand, ShowAllConfigCommand>()
            .AddSingleton<IChatCommand, ShowCensorWordsCommand>()
            .AddSingleton<IChatCommand, ShowCommandStatusesCommand>()
            .AddSingleton<IChatCommand, ShowComplaintsCommand>()
            .AddSingleton<IChatCommand, ShutdownCommand>()
            .AddSingleton<IChatCommand, ToggleCommandStatusCommand>();
    }

    /// <summary>
    ///     Регистрация платформенных сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterChatServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<MessageProcessingService>()
            .AddSingleton<DiscordImageProcessor>()
            .AddSingleton<DiscordMessageHandler>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<DiscordMessageHandler>())
            .AddSingleton<TelegramImageProcessor>()
            .AddSingleton<TelegramMessageHandler>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TelegramMessageHandler>())
            .AddSingleton<DiscordMessageDataProcessor>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<DiscordMessageDataProcessor>())
            .AddSingleton<TelegramMessageDataProcessor>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TelegramMessageDataProcessor>())
            .AddSingleton<TwitchMessageDataProcessor>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TwitchMessageDataProcessor>())
            .AddSingleton<VkPlayMessageDataProcessor>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<VkPlayMessageDataProcessor>())
            .AddSingleton<TelegramChannelManager>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TelegramChannelManager>())
            .AddSingleton<VkPlayMessageSender>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<VkPlayMessageSender>())
            .AddSingleton<TwitchImageProcessor>()
            .AddSingleton<TwitchMessageHandler>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TwitchMessageHandler>())
            .AddSingleton<VkPlayImageProcessor>()
            .AddSingleton<VkPlayMessageHandler>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<VkPlayMessageHandler>())
            .AddSingleton<DiscordGuildChannelService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<DiscordGuildChannelService>());
    }

    /// <summary>
    ///     Регистрация сервисов команд для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterCommandManagementServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<CommandStatusService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<CommandStatusService>())
            .AddSingleton<CommandsListService>()
            .AddSingleton<CommandExecutionService>()
            .AddSingleton<CommandParser>()
            .AddSingleton<ICommandParser, CommandParser>()
            .AddSingleton<CooldownService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<CooldownService>())
            .AddSingleton<ExtraCommandsService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ExtraCommandsService>());
    }

    /// <summary>
    ///     Регистрация команд чата, реализующих интерфейс <see cref="IChatCommand" />.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с зарегистрированными командами чата.</returns>
    public static IServiceCollection RegisterCommands(this IServiceCollection services)
    {
        return services
            .RegisterAdminCommands()
            .RegisterUserCommands();
    }

    /// <summary>
    ///     Регистрация основных сервисов для ядра приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с зарегистрированными основными сервисами.</returns>
    public static IServiceCollection RegisterCoreServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ICommandRegistry, CommandRegistry>()
            .AddSingleton<ChatBot>()
            .AddSingleton<TelegramChatService>()
            .AddSingleton<IChatService>(sp => sp.GetRequiredService<TelegramChatService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TelegramChatService>())
            .AddSingleton<TwitchChatService>()
            .AddSingleton<IChatService>(sp => sp.GetRequiredService<TwitchChatService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TwitchChatService>())
            .AddSingleton<DiscordChatService>()
            .AddSingleton<IChatService>(sp => sp.GetRequiredService<DiscordChatService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<DiscordChatService>())
            .AddSingleton<VkPlayChatService>()
            .AddSingleton<IChatService>(sp => sp.GetRequiredService<VkPlayChatService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<VkPlayChatService>());
    }

    /// <summary>
    ///     Регистрация Google сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterGoogleServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<GoogleSearchService>()
            .AddSingleton<IGoogleSearchService>(sp => sp.GetRequiredService<GoogleSearchService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<GoogleSearchService>());
    }

    /// <summary>
    ///     Регистрация Google Sheets сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterGoogleSheetsServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<GoogleSheetsBaseService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<GoogleSheetsBaseService>())
            .AddScoped<GameGoogleSheetsService>()
            .AddScoped<RateGoogleSheetsService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<RateGoogleSheetsService>())
            .AddScoped<StreamGoogleSheetsService>();
    }

    /// <summary>
    ///     Регистрация медиа сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterMediaServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ImageSearchService>()
            .AddSingleton<CatImageService>()
            .AddSingleton<GifSearchService>()
            .AddSingleton<VideoSearchService>()
            .AddSingleton<ImageGeneratorService>()
            .AddSingleton<IImageGeneratorService>(sp => sp.GetRequiredService<ImageGeneratorService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ImageGeneratorService>());
    }

    /// <summary>
    ///     Регистрация нейросетевых сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterNeuralNetworkServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ChatGptService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ChatGptService>())
            .AddSingleton<GeminiSettingsProvider>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<GeminiSettingsProvider>())
            .AddSingleton<ChatGeminiService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ChatGeminiService>())
            .AddSingleton<AskGeminiService>()
            .AddSingleton<GeminiImageGenerationService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ImageGenerationService>())
            .AddSingleton<ImageGenerationService>();
    }

    /// <summary>
    ///     Регистрация остальных сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterOtherServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<HolidaysService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<HolidaysService>())
            .AddSingleton<GameProgressService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<GameProgressService>())
            .AddSingleton<FactService>()
            .AddSingleton<TranslationService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TranslationService>())
            .AddSingleton<DivergenceService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<DivergenceService>())
            .AddSingleton<ComplaintService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ComplaintService>())
            .AddSingleton<HowLongToBeatService>()
            .AddSingleton<ExchangeRateService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ExchangeRateService>())
            .AddSingleton<MailRuAnswerService>()
            .AddSingleton<ClipsService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ClipsService>())
            .AddSingleton<WisdomService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<WisdomService>())
            .AddSingleton<StreamChatterStatsService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<StreamChatterStatsService>())
            .AddSingleton<TextCommandService>();
    }

    /// <summary>
    ///     Регистрация таймерных сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterScheduledTaskServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ScheduledTaskService>()
            .AddSingleton<TelegramTasksService>()
            .AddSingleton<BirthdayTelegramNotificationService>()
            .AddSingleton<CommonTasksService>()
            .AddSingleton<TwitchStreamMonitoringService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<TwitchStreamMonitoringService>());
    }

    /// <summary>
    ///     Регистрация всех сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        return services
            .RegisterCoreServices()
            .RegisterChatServices()
            .RegisterCommandManagementServices()
            .RegisterGoogleServices()
            .RegisterGoogleSheetsServices()
            .RegisterMediaServices()
            .RegisterNeuralNetworkServices()
            .RegisterScheduledTaskServices()
            .RegisterUserManagementServices()
            .RegisterUtilityServices()
            .RegisterOtherServices();
    }

    /// <summary>
    ///     Регистрация пользовательских команд чата.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с зарегистрированными командами чата.</returns>
    public static IServiceCollection RegisterUserCommands(this IServiceCollection services)
    {
        return services
            .AddSingleton<IChatCommand, AnimeWisdomCommand>()
            .AddSingleton<IChatCommand, AddBirthdayCommand>()
            //.AddSingleton<IChatCommand, AskCommand>()
            .AddSingleton<IChatCommand, AskGeminiCommand>()
            .AddSingleton<IChatCommand, AskGeminiWithGoogleSearchCommand>()
            .AddSingleton<IChatCommand, AskMailRuCommand>()
            .AddSingleton<IChatCommand, BirthdayCommand>()
            .AddSingleton<IChatCommand, CallCommand>()
            .AddSingleton<IChatCommand, ComplaintCommand>()
            .AddSingleton<IChatCommand, CatCommand>()
            .AddSingleton<IChatCommand, ClipCommand>()
            .AddSingleton<IChatCommand, CommandsListCommand>()
            .AddSingleton<IChatCommand, DefinitionCommand>()
            .AddSingleton<IChatCommand, ExecuteExtraCommand>()
            .AddSingleton<IChatCommand, AlmightyCommand>()
            .AddSingleton<IChatCommand, DivergenceCommand>()
            .AddSingleton<IChatCommand, FactCommand>()
            .AddSingleton<IChatCommand, GameInfoCommand>()
            .AddSingleton<IChatCommand, GameProgressCommand>()
            .AddSingleton<IChatCommand, GameRatingCommand>()
            .AddSingleton<IChatCommand, GifCommand>()
            .AddSingleton<IChatCommand, GoogleSearchWithNeuralNetworkCommand>()
            .AddSingleton<IChatCommand, HoroscopeCommand>()
            .AddSingleton<IChatCommand, ImageCommand>()
            .AddSingleton<IChatCommand, SpoilerImageCommand>()
            .AddSingleton<IChatCommand, ImageGenerateCommand>()
            .AddSingleton<IChatCommand, GeminiImageGenerateCommand>()
            .AddSingleton<IChatCommand, LikenessCommand>()
            .AddSingleton<IChatCommand, MbtiCommand>()
            .AddSingleton<IChatCommand, MultipleImageCommand>()
            .AddSingleton<IChatCommand, MultipleSpoilerImageCommand>()
            .AddSingleton<IChatCommand, RemindCommand>()
            .AddSingleton<IChatCommand, RemoveBirthdayCommand>()
            .AddSingleton<IChatCommand, SbtiCommand>()
            .AddSingleton<IChatCommand, TranslateCommand>()
            .AddSingleton<IChatCommand, VideoCommand>()
            .AddSingleton<IChatCommand, WhoMbtiCommand>()
            .AddSingleton<IChatCommand, WhoSbtiCommand>()
            .AddSingleton<IChatCommand, WisdomCommand>()
            .AddSingleton<IChatCommand, HolidayCommand>()
            .AddSingleton<MentionCommand>()
            .AddSingleton<IChatCommand>(sp => sp.GetRequiredService<MentionCommand>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<MentionCommand>());
    }

    /// <summary>
    ///     Регистрация сервисов по управлению пользовательскими данными для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterUserManagementServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<BirthdayService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<BirthdayService>())
            .AddSingleton<MbtiService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<MbtiService>())
            .AddSingleton<JsonMbtiCharacterCatalogService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<JsonMbtiCharacterCatalogService>())
            .AddSingleton<SbtiService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<SbtiService>())
            .AddSingleton<JsonSbtiCharacterCatalogService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<JsonSbtiCharacterCatalogService>())
            .AddSingleton<RoleService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<RoleService>());
    }

    /// <summary>
    ///     Регистрация утилитных сервисов для приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов для регистрации.</param>
    /// <returns>Коллекция сервисов с добавленными сервисами.</returns>
    public static IServiceCollection RegisterUtilityServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<AutoTranslateService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<AutoTranslateService>())
            .AddSingleton<CensorWordsService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<CensorWordsService>())
            .AddSingleton<CensorshipService>()
            .AddSingleton<ICensorshipService>(sp => sp.GetRequiredService<CensorshipService>())
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<CensorshipService>())
            .AddSingleton<ConfigService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<ConfigService>())
            .AddSingleton<IProcessController, ProcessController>()
            .AddSingleton<UrlShortenerService>()
            .AddSingleton<LayoutCorrectionService>()
            .AddSingleton<IAsyncInitializable>(sp => sp.GetRequiredService<LayoutCorrectionService>())
            .AddSingleton<WebContentService>();
    }
}