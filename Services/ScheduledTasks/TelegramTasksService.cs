using AbsoluteBot.Chat.Context;
using AbsoluteBot.Helpers;
using AbsoluteBot.Services.ChatServices.TelegramChat;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using Serilog;

namespace AbsoluteBot.Services.ScheduledTasks;

/// <summary>
///     Сервис для выполнения ежедневных задач в Telegram, таких как отправка информации о праздниках и курсе валют.
/// </summary>
public class TelegramTasksService(TelegramChannelManager telegramChannelManager, TelegramChatService telegramChatService,
    HolidaysService holidaysService, INeuralAskService neuralAsk, ExchangeRateService exchangeRateService)
{
    private const string DateFormat = "dd.MM";
    internal const int MaxFactLength = 450;
    internal const int MaxPostLength = 700;

    internal const string HolidayInstruction =
        "Короткий ответ: 3–6 предложений, строго не больше 450 символов. " +
        "Один малоизвестный факт в духе «а ты знал, что…». Без заголовков #, без списков, без «Короткий итог», без длинной статьи. " +
        "Можно выделить 1–2 слова **жирным**. Не используй одинарные * для курсива.";

    /// <summary>
    ///     Выполняет ежедневные задачи в Telegram, такие как отправка информации о праздниках и курсе валют.
    /// </summary>
    public async Task ExecuteDailyTelegramTask()
    {
        try
        {
            var channelId = await telegramChannelManager.GetTelegramChannelId(ChannelType.Premium).ConfigureAwait(false);

            await HolidayHandler(channelId).ConfigureAwait(false);

            await ChartHandler(channelId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выполнении ежедневных задач в Telegram.");
        }
    }

    internal static string BuildHolidayHtml(string holiday, string? fact)
    {
        var trimmed = string.IsNullOrWhiteSpace(fact) ? string.Empty : fact.Trim();
        if (trimmed.Length > MaxFactLength)
            trimmed = TextProcessingUtils.CutSentence(trimmed, MaxFactLength);
        return TelegramHtmlFormatter.Compose($"Сегодня праздник: {holiday}", trimmed, MaxPostLength);
    }

    /// <summary>
    ///     Отправляет график курса валют в Telegram.
    /// </summary>
    /// <param name="channelId">Идентификатор канала в Telegram.</param>
    private async Task ChartHandler(long channelId)
    {
        var chartUrl = await exchangeRateService.GetExchangeRateChartUrlAsync().ConfigureAwait(false);
        if (chartUrl == null) return;
        await telegramChatService.SendPhotoToChannelAsync(chartUrl, channelId.ToString()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Отправляет информацию о празднике и интересный факт в Telegram.
    /// </summary>
    /// <param name="channelId">Идентификатор канала в Telegram.</param>
    private async Task HolidayHandler(long channelId)
    {
        var today = DateTime.Today.ToString(DateFormat);
        var holiday = holidaysService.GetHoliday(today);
        var fact = await neuralAsk.AskAsync(
                $"Расскажи один короткий малоизвестный факт про праздник «{holiday}» ({today}).",
                MaxFactLength,
                instruction: HolidayInstruction,
                temperature: 0.8)
            .ConfigureAwait(false);
        var html = BuildHolidayHtml(holiday, fact);
        await telegramChatService.SendHtmlMessageToChannelAsync(html, channelId.ToString()).ConfigureAwait(false);
    }
}
