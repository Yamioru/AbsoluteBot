using System.Collections.Concurrent;
using System.Text;
using AbsoluteBot.Services.ChatServices.TwitchChat;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.Recap;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
/// Совет по текущей игре: факты стрима, кадр Twitch и Gemini с поиском.
/// </summary>
public sealed class StreamAdviceService(
    ConfigService configService,
    TwitchChatService twitchChatService,
    TwitchFrameGrabber frameGrabber,
    GameProgressService gameProgressService,
    ChatJournalService chatJournal,
    StreamGoogleSheetsService streamSheets,
    GeminiAskClient geminiAsk,
    NeuralModelConfigService modelConfig,
    ICensorshipService censorshipService)
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(90);
    private readonly ConcurrentDictionary<string, DateTime> _lastCall = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> AdviseAsync(string userKey, string? question, int maxLength)
    {
        if (_lastCall.TryGetValue(userKey, out var previous))
        {
            var left = Cooldown - (DateTime.UtcNow - previous);
            if (left > TimeSpan.Zero)
                return $"Подожди ещё {(int) Math.Ceiling(left.TotalSeconds)} с.";
        }

        string? game;
        var online = false;
        try
        {
            var stream = await twitchChatService.GetStreamState().ConfigureAwait(false);
            online = stream != null;
            game = online ? stream!.GameName : await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Состояние стрима для совета не прочиталось.");
            game = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(game))
            return "Не вижу, во что сейчас играют.";

        _lastCall[userKey] = DateTime.UtcNow;
        gameProgressService.TryGetPlaytime(game, out var minutes, out var percent);
        var occurrence = await CountGameStreamsAsync(game).ConfigureAwait(false);

        string? image = null;
        if (online)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            image = await frameGrabber.CaptureJpegBase64Async(timeout.Token).ConfigureAwait(false);
        }

        var facts = new StringBuilder();
        facts.Append("Игра: ").Append(game).AppendLine(".");
        facts.AppendLine(online
            ? "Это идёт на стриме, прохождение медленнее, чем в соло. Нужен один ближайший шаг, не глава гайда."
            : "Стрим сейчас выключен. Это последняя игра, кадра нет.");
        if (percent >= 0) facts.Append("Общий прогресс около ").Append(percent.ToString("0.#")).AppendLine("%.");
        if (minutes > 0) facts.Append("Наиграно примерно ").Append(minutes / 60).Append(" ч ").Append(minutes % 60).AppendLine(" мин.");
        if (occurrence > 0) facts.Append("По этой игре это уже стрим номер ").Append(occurrence).AppendLine(".");
        if (string.IsNullOrWhiteSpace(question))
            facts.AppendLine("Отдельного вопроса нет: совет по тому, что на кадре и по стадии игры.");
        else
            facts.Append("Вопрос зрителя важнее кадра, отвечай на него: ").AppendLine(question.Trim());
        if (image == null) facts.AppendLine("Кадра нет. Не описывай картинку, которой не видишь.");

        const string instruction =
            "Дай конкретный совет по этой игре. Не спойлерь сюжет, концовки и кто есть кто. Механика, предмет, куда встать и слабое место — можно. Запрещены общие фразы вроде «уклоняйтесь и наносите урон» и «используйте предметы». Если конкретного совета нет, так и скажи. Без markdown.";

        if (!geminiAsk.HasApiKeys)
            return "Не получилось спросить совет.";

        var free = modelConfig.IsFreeTier();
        var entity = free ? NeuralEntities.Ask : NeuralEntities.GoogleSearch;
        var answer = await NeuralModelAttempts.RunAsync(
            modelConfig.GetGeminiAttemptModels(entity),
            entity,
            model => geminiAsk.AskAsync(facts.ToString(), instruction, image, model, 0.4, !free)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(answer))
            return "Не получилось получить совет.";
        return censorshipService.ApplyCensorship(answer, Math.Max(80, maxLength), false);
    }

    private async Task<int> CountGameStreamsAsync(string game)
    {
        var numbers = new HashSet<int>();
        foreach (var session in await chatJournal.ReadSessionsAsync().ConfigureAwait(false))
            if (session.Games.Any(g => g.Name.Equals(game, StringComparison.OrdinalIgnoreCase)))
                numbers.Add(session.Number);

        foreach (var row in await streamSheets.ListStreamsAsync().ConfigureAwait(false))
            if (row.Games.Any(g => g.Equals(game, StringComparison.OrdinalIgnoreCase)))
                numbers.Add(row.Number);

        return numbers.Count;
    }
}