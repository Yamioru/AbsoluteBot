namespace AbsoluteBot.Services.GoogleSearch;

/// <summary>
///     Определяет интерфейс для выполнения поисковых запросов и получения определения термина.
/// </summary>
public interface IGoogleSearchDefinitionService
{
    /// <summary>
    ///     Выполняет поиск и возвращает определение для указанного запроса.
    /// </summary>
    /// <param name="query">Поисковый запрос для определения.</param>
    /// <param name="maxLength">Максимальная длина возвращаемого определения.</param>
    /// <returns>
    ///     Строка с определением запроса, если оно найдено, или null, если определение не было найдено.
    /// </returns>
    Task<string?> GetDefinitionAsync(string query, int maxLength);
}