using System.Text.Json;
using System.Text.RegularExpressions;
using AbsoluteBot.Models;
using Serilog;

namespace AbsoluteBot.Services;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для поиска ответов на вопросы через Mail.ru Ответы.
/// </summary>
public partial class MailRuAnswerService(HttpClient httpClient)
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36";
    private const string AcceptLanguage = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const string GoogleSearchUrlTemplate = "https://www.google.com/search?safe=active&q={0}+otvet.mail.ru";
    private const string MailRuApiUrlTemplate = "https://otvet.mail.ru/api/v2/question?qid=";
    private const int MaxCacheSize = 100;
    private readonly HashSet<string> _oldAnswers = new();
    private readonly Random _random = new();

    /// <summary>
    ///     Выполняет запрос на поиск ответа через Google и извлекает ответ с Mail.ru Ответы.
    /// </summary>
    /// <param name="query">Вопрос, который нужно задать.</param>
    /// <returns>Ответ на вопрос, либо сообщение об отсутствии ответа.</returns>
    public virtual async Task<string?> AskAsync(string query)
    {
        try
        {
            query = FormatQueryForSearch(query);

            // Поиск через  Google
            var googleSearchUrl = string.Format(GoogleSearchUrlTemplate, query);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                UserAgent);
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(AcceptLanguage);
            var googleResponse = await httpClient.GetStringAsync(googleSearchUrl).ConfigureAwait(false);

            // Извлечение ссылки на вопрос Mail.ru
            var questionUrl = ExtractQuestionUrl(googleResponse);
            if (string.IsNullOrEmpty(questionUrl))
                return "Ответа нет!";

            var questionId = ExtractQuestionId(questionUrl);
            if (string.IsNullOrEmpty(questionId))
                return "Ответа нет!";

            // Запрос данных с Mail.ru
            var mailRuApiUrl = $"{MailRuApiUrlTemplate}{questionId}";
            var mailRuResponse = await httpClient.GetStringAsync(mailRuApiUrl).ConfigureAwait(false);
            var mailRuAnswer = JsonSerializer.Deserialize<MailRuAnswer>(mailRuResponse);
            if (mailRuAnswer == null) return null;

            // Обработка ответа
            var answer = FilterAndSelectAnswer(mailRuAnswer, _random);

            return answer;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка во время получения ответа от mail ru otvet.");
            return null;
        }
    }

    /// <summary>
    ///     Добавляет ответ в кэш, удаляя старые ответы, если размер кэша превышает ограничение.
    /// </summary>
    /// <param name="answer">Ответ для добавления в кэш.</param>
    private void AddToCache(string answer)
    {
        if (_oldAnswers.Count >= MaxCacheSize)
            // Удаление старейшего элемента из кэша
            _oldAnswers.Remove(_oldAnswers.Last());

        _oldAnswers.Add(answer);
    }

    /// <summary>
    ///     Извлекает идентификатор вопроса из URL.
    /// </summary>
    /// <param name="questionUrl">URL вопроса.</param>
    /// <returns>Идентификатор вопроса или <c>null</c>, если идентификатор не найден.</returns>
    private static string? ExtractQuestionId(string questionUrl)
    {
        var match = QuestionIdRegex().Match(questionUrl);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    ///     Извлекает ссылку на вопрос Mail.ru из результатов Google поиска.
    /// </summary>
    /// <param name="response">Ответ Google поиска.</param>
    /// <returns>URL вопроса или <c>null</c>, если ссылка не найдена.</returns>
    private static string? ExtractQuestionUrl(string response)
    {
        var match = MailRuUrlRegex().Match(response);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    ///     Обрабатывает ответы на вопрос, выбирает наиболее подходящий и исключает уже использованные ответы.
    /// </summary>
    /// <param name="mailRuAnswer">Ответы, полученные с Mail.ru.</param>
    /// <param name="random">Объект для случайного выбора ответов при необходимости.</param>
    /// <returns>Наиболее подходящий ответ или <c>null</c>, если нет доступных ответов.</returns>
    private string? FilterAndSelectAnswer(MailRuAnswer mailRuAnswer, Random random)
    {
        // Собираются все ответы, включая лучший, если он есть
        var answers = new List<Answer>();
        if (mailRuAnswer.BestAnswer != null) answers.Add(mailRuAnswer.BestAnswer);

        answers.AddRange(mailRuAnswer.Answers.Where(a => !string.IsNullOrEmpty(a.Text)));

        if (answers.Count == 0) return null;

        // Исключение старых ответов
        var newAnswers = answers
            .Where(ans => !_oldAnswers.Contains(ans.Text))
            .OrderByDescending(ans => ans.Likes)
            .ToList();

        // Выбор ответа
        string answer;
        if (newAnswers.Count != 0)
            answer = newAnswers.First().Text;
        else
            answer = answers.OrderByDescending(ans => ans.Likes)
                .ThenBy(_ => random.Next())
                .First().Text;

        // Добавление ответа в список использованных
        AddToCache(answer);

        if (!answer.EndsWith('.') && !answer.EndsWith('!') && !answer.EndsWith('?')) answer += ".";
        answer += " Также есть другие ответы...";

        return answer;
    }

    /// <summary>
    ///     Обрабатывает запрос перед отправкой, приводит к нижнему регистру и заменяет пробелы на плюсы.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <returns>Обработанный запрос.</returns>
    private static string FormatQueryForSearch(string query)
    {
        query = query.ToLower().Trim();
        return query.Replace(" ", "+");
    }

    [GeneratedRegex(@"https://otvet.mail.ru/question/\d+")]
    private static partial Regex MailRuUrlRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex QuestionIdRegex();
}