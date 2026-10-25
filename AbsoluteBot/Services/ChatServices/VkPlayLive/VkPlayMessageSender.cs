using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
/// Отвечает за отправку сообщений в VkPlayLive через HTTP-запросы.
/// </summary>
public partial class VkPlayMessageSender(ConfigService configService, HttpClient httpClient, VkPlayAuthService? authService = null)
    : IAsyncInitializable
{
    private const string VkPlayApiUrlFormat = "https://api.live.vkplay.ru/v1/blog/{0}/public_video_stream/chat";
    private const string TextBlockType = "text";
    private const string ContentType = "application/x-www-form-urlencoded";
    private const string LinkBlockType = "link";
    private const string UnstyledText = "unstyled";
    private string? _authSendToken;
    private string? _channelName;

    public async Task InitializeAsync()
    {
        _authSendToken = await configService.GetConfigValueAsync<string>("VkPlayAuthSendToken").ConfigureAwait(false);
        _channelName = await configService.GetConfigValueAsync<string>("VkPlayChannelName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(ResolveSendToken()) || string.IsNullOrEmpty(_channelName))
            Log.Warning("Не удалось данные для отправки сообщений в vkplaylive.");
    }

    /// <summary>
    /// Асинхронно отправляет сообщение в VkPlayLive через HTTP POST-запрос.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="messageId">Идентификатор сообщения, на которое нужно ответить, если не нужны отвечать, то не указывать.</param>
    public async Task PostMessageAsync(string message, int messageId = 0)
    {
        var token = ResolveSendToken();
        if (token == null || _channelName == null) return;

        using var response = await SendOnceAsync(message, messageId, token).ConfigureAwait(false);
        if (!IsUnauthorized(response))
        {
            response.EnsureSuccessStatusCode();
            return;
        }

        if (authService != null && await authService.TryRefreshAsync().ConfigureAwait(false))
        {
            var refreshed = ResolveSendToken();
            if (!string.IsNullOrEmpty(refreshed))
            {
                _authSendToken = refreshed;
                using var retry = await SendOnceAsync(message, messageId, refreshed).ConfigureAwait(false);
                if (!IsUnauthorized(retry))
                {
                    retry.EnsureSuccessStatusCode();
                    return;
                }
            }
        }

        if (authService != null)
        {
            await authService.NotifyAuthFailedAsync().ConfigureAwait(false);
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    ///     Сериализует сообщение для VkPlay (доступно тестам через InternalsVisibleTo).
    /// </summary>
    internal static string SerializeMessageForTests(string message) => SerializeMessage(message);

    public void SetAuthSendToken(string token)
    {
        _authSendToken = token;
    }

    private string? ResolveSendToken()
    {
        if (!string.IsNullOrEmpty(authService?.AccessToken)) return authService.AccessToken;
        return _authSendToken;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(string message, int messageId, string token)
    {
        var url = string.Format(VkPlayApiUrlFormat, _channelName);
        var data = "data=" + SerializeMessage(message);
        if (messageId > 0) data += $"&reply_to_id={messageId}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        request.Content = new StringContent(data, Encoding.UTF8, ContentType);
        return await httpClient.SendAsync(request).ConfigureAwait(false);
    }

    private static bool IsUnauthorized(HttpResponseMessage response)
    {
        return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
    }

    /// <summary>
    /// Формирует контент для блока сообщения в зависимости от его типа.
    /// </summary>
    /// <param name="message">Сообщение или текст, который нужно включить в блок.</param>
    /// <param name="type">Тип блока сообщения (например, "unstyled" для обычного текста или "link" для ссылки).</param>
    /// <returns>Сериализованная строка в формате JSON, которая представляет содержимое блока сообщения.</returns>
    private static string GenerateContent(string message, string type)
    {
        return JsonSerializer.Serialize(new object[] {message, type, Array.Empty<object>()});
    }

    /// <summary>
    /// Сериализует сообщение в JSON-формат для отправки в VkPlayLive. Определяет, является ли сообщение текстом, ссылкой
    /// или их комбинацией.
    /// </summary>
    /// <param name="message">Текст сообщения или URL.</param>
    /// <returns>Сериализованное сообщение в формате JSON.</returns>
    private static string SerializeMessage(string message)
    {
        var messageBlocks = new List<MessageBlock>();

        // Поиск ссылок в тексте
        var matches = UrlRegex().Matches(message);

        if (matches.Count == 0)
        {
            // Сообщение состоит только из текста
            messageBlocks.Add(new MessageBlock
            {
                Type = TextBlockType,
                Content = GenerateContent(message, UnstyledText)
            });
        }
        else
        {
            // Обработка комбинации текста и ссылок
            var lastIndex = 0;
            foreach (var match in matches.Cast<Match>())
            {
                // Добавление текстового блока перед ссылкой, если есть текст до ссылки
                if (match.Index > lastIndex)
                {
                    var textPart = message[lastIndex..match.Index];
                    messageBlocks.Add(new MessageBlock
                    {
                        Type = TextBlockType,
                        Content = GenerateContent(textPart, UnstyledText)
                    });
                }

                // Добавление блока ссылки
                messageBlocks.Add(new MessageBlock
                {
                    Type = LinkBlockType,
                    Content = GenerateContent(match.Value, UnstyledText),
                    Url = match.Value
                });

                // Обновление индекса для следующей итерации
                lastIndex = match.Index + match.Length;
            }

            // Добавление оставшегося текста после последней ссылки, если есть
            if (lastIndex < message.Length)
            {
                var remainingText = message[lastIndex..];
                messageBlocks.Add(new MessageBlock
                {
                    Type = TextBlockType,
                    Content = GenerateContent(remainingText, UnstyledText)
                });
            }
        }

        return JsonSerializer.Serialize(messageBlocks);
    }

    [GeneratedRegex(@"(https?:\/\/[^\s]+)")]
    private static partial Regex UrlRegex();
}