namespace AbsoluteBot.Services.GoogleSearch;

/// <summary>
///     Определяет интерфейс для сервиса выполнения поисковых запросов в Google.
/// </summary>
public interface IGoogleSearchService
{
    /// <summary>
    ///     Выполняет поиск в Google по заданному запросу и возвращает список ссылок на найденные ресурсы.
    /// </summary>
    /// <param name="query">Запрос для поиска.</param>
    /// <param name="linkCount">Количество ссылок, которые должны быть возвращены (по умолчанию 5).</param>
    /// <param name="siteSearchValue">Сайт на котором надо искать</param>
    /// <returns>Список строк с URL-адресами найденных ресурсов или <c>null</c>, если ничего не найдено.</returns>
    Task<List<string>?> PerformSearchAsync(string query, int linkCount = 5, string? siteSearchValue = null);
}