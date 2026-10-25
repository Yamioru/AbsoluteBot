namespace AbsoluteBot.Models;

/// <summary>
///     Представляет ответ на запрос Google поиска, содержащий список элементов поиска.
/// </summary>
public class GoogleSearchResponse
{
    /// <summary>
    ///     Список элементов поиска.
    /// </summary>
    public required List<SearchItem> Items { get; set; }
}

/// <summary>
///     Представляет отдельный элемент поиска, содержащий ссылку на найденный ресурс.
/// </summary>
public class SearchItem
{
    /// <summary>
    ///     Ссылка на ресурс, найденный в результате поиска.
    /// </summary>
    public required string Link { get; set; }
}