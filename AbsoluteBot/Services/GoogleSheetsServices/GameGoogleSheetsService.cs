using System.Text.RegularExpressions;
using FuzzySharp;
using Google.Apis.Sheets.v4.Data;
using Serilog;

namespace AbsoluteBot.Services.GoogleSheetsServices;

#pragma warning disable IDE0028
/// <summary>
///     Класс для работы с Google Sheets, связанный с играми на стриме.
/// </summary>
public class GameGoogleSheetsService(GoogleSheetsBaseService googleSheetsBaseService)
{
    private const string GameSheetName = "Игры"; // Название листа со списком игр
    private const string GameNumberColumnLetter = "A"; // Столбец номеров игр
    private const string GameNameColumnLetter = "B"; // Столбец названий игр
    private const string GameProgressColumnLetter = "C"; // Столбец прогресса прохождения игр
    private const string GameStreamNumbersColumnLetter = "D"; // Столбец номеров стримов для игры
    private const string GameChatRateColumnLetter = "G"; // Столбец оценки чата для игр
    private const int StartGameRow = 13; // Строка с которой начинается список игр
    private const int FirstColumnIndex = 0; // Первый индекс столбца с котором начинается список игр
    private const int PreStartGameRow = StartGameRow - 1; // Строка после которой начинается список игр
    private static readonly Color EvenRowBackgroundColor = new() {Red = 0.6431f, Green = 0.7608f, Blue = 0.9569f};
    private static readonly Color OddRowBackgroundColor = new() {Red = 0.7882f, Green = 0.8549f, Blue = 0.9725f};

    /// <summary>
    ///     Добавляет новую игру или обновляет номер стрим у найденной игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="streamNumber">Номер стрима.</param>
    public async Task AddOrUpdateGameWithStreamAsync(string gameName, int streamNumber)
    {
        try
        {
            if (googleSheetsBaseService.SheetsService == null)
            {
                Log.Warning("Google Sheets Service не работает и равен null.");
                return;
            }

            var gameIndex = await FindGameRowAsync(gameName).ConfigureAwait(false);
            if (gameIndex != -1)
                await UpdateStreamNumberAsync(gameIndex, streamNumber).ConfigureAwait(false);
            else
                await AddGameToTopAsync(gameName, streamNumber).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при нахождении игры и добавлении записи стрима или при добавлении игры.");
        }
    }

    /// <summary>
    ///     Получает информацию об игре по её названию.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Информация об игре, если найдена; иначе null.</returns>
    public virtual async Task<string?> FetchGameInfoAsync(string gameName)
    {
        try
        {
            if (googleSheetsBaseService.SheetsService == null)
            {
                Log.Warning("Google Sheets Service не работает и равен null.");
                return null;
            }

            gameName = Regex.Unescape(gameName);

            if (IsGameNameInvalid(gameName))
                return null;

            var gameList = await GetGameListValuesAsync().ConfigureAwait(false);

            return await FindAndReturnGameDetailsAsync(gameList, gameName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении информации об игре.");
            return null;
        }
    }

    /// <summary>
    ///     Обновляет прогресс игры по её названию.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="percentage">Процент завершённости игры.</param>
    public async Task UpdateGameProgressAsync(string gameName, string percentage)
    {
        try
        {
            if (googleSheetsBaseService.SheetsService == null)
            {
                Log.Warning("Google Sheets Service не работает и равен null.");
                return;
            }

            // Поиск строки игры в таблице.
            var gameRow = await FindGameRowAsync(gameName).ConfigureAwait(false);
            if (gameRow == -1)
            {
                Log.Warning("Не удалось найти игру для обновления прогресса.");
                return;
            }

            // Обновление значения прогресса в таблице.
            var range = $"{GameSheetName}!{GameProgressColumnLetter}{gameRow}";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> {new List<object> {percentage}}
            };
            await googleSheetsBaseService.UpdateValuesAsync(range, valueRange).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении прогресса игры в таблице Google Sheets.");
        }
    }

    /// <summary>
    ///     Вставляет игру в начало таблицы с соответствующими данными.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="streamNumber">Номер стрима.</param>
    private async Task AddGameToTopAsync(string gameName, int streamNumber)
    {
        var sheetId = await googleSheetsBaseService.GetSheetIdByNameAsync(GameSheetName).ConfigureAwait(false);
        if (sheetId < 0)
        {
            Log.Warning("Не удалось найти лист по его названию");
            return;
        }

        var rowGameNumber = await GetNextGameNumberAsync().ConfigureAwait(false);

        var backgroundColor = GetRowBackgroundColor(rowGameNumber);

        var requests = new List<Request>
        {
            CreateInsertRowRequest(sheetId),
            CreateUpdateCellsRequest(sheetId, rowGameNumber, gameName, streamNumber)
        };
        requests.AddRange(CreateFormatRowRequest(sheetId, backgroundColor));

        await googleSheetsBaseService.ExecuteBatchUpdateAsync(requests).ConfigureAwait(false);
    }

    /// <summary>
    ///     Создаёт запрос для форматирования ячеек в определённом диапазоне столбцов в строке.
    /// </summary>
    /// <param name="sheetId">Идентификатор листа Google Sheets.</param>
    /// <param name="startColumnIndex">Начальный индекс столбца.</param>
    /// <param name="endColumnIndex">Конечный индекс столбца.</param>
    /// <param name="backgroundColor">Цвет фона для форматирования ячеек.</param>
    /// <returns>Запрос для форматирования ячеек в заданных столбцах.</returns>
    private static Request CreateFormatCellRequest(int sheetId, int startColumnIndex, int endColumnIndex,
        Color backgroundColor)
    {
        return new Request
        {
            RepeatCell = new RepeatCellRequest
            {
                Range = new GridRange
                {
                    SheetId = sheetId,
                    StartRowIndex = PreStartGameRow,
                    EndRowIndex = StartGameRow,
                    StartColumnIndex = startColumnIndex,
                    EndColumnIndex = endColumnIndex
                },
                Cell = new CellData
                {
                    UserEnteredFormat = new CellFormat
                    {
                        BackgroundColor = backgroundColor
                    }
                },
                Fields = "userEnteredFormat.backgroundColor"
            }
        };
    }

    /// <summary>
    ///     Создаёт запрос для форматирования строки в Google Sheets.
    /// </summary>
    /// <param name="sheetId">Идентификатор листа Google Sheets.</param>
    /// <param name="backgroundColor">Цвет фона для форматирования строки.</param>
    /// <returns>Запрос для форматирования строки.</returns>
    private static List<Request> CreateFormatRowRequest(int sheetId, Color backgroundColor)
    {
        return new List<Request>
        {
            CreateFormatCellRequest(sheetId, 1, 2, backgroundColor),
            CreateFormatCellRequest(sheetId, 3, 7, backgroundColor)
        };
    }

    /// <summary>
    ///     Создаёт запрос на вставку новой строки в таблицу Google Sheets.
    /// </summary>
    /// <param name="sheetId">Идентификатор листа Google Sheets.</param>
    /// <returns>Запрос на вставку строки.</returns>
    private static Request CreateInsertRowRequest(int sheetId)
    {
        return new Request
        {
            InsertDimension = new InsertDimensionRequest
            {
                Range = new DimensionRange
                {
                    SheetId = sheetId,
                    Dimension = "ROWS",
                    StartIndex = PreStartGameRow,
                    EndIndex = StartGameRow
                },
                InheritFromBefore = false
            }
        };
    }

    /// <summary>
    ///     Создаёт запрос на обновление ячеек в таблице Google Sheets.
    /// </summary>
    /// <param name="sheetId">Идентификатор листа Google Sheets.</param>
    /// <param name="rowGameNumber">Номер строки для вставки игры в таблицу.</param>
    /// <param name="gameName">Название игры для вставки в таблицу.</param>
    /// <param name="streamNumber">Номер стрима для вставки в таблицу.</param>
    /// <returns>Запрос на обновление ячеек.</returns>
    private static Request CreateUpdateCellsRequest(int sheetId, int rowGameNumber, string gameName, int streamNumber)
    {
        return new Request
        {
            UpdateCells = new UpdateCellsRequest
            {
                Start = new GridCoordinate
                {
                    SheetId = sheetId,
                    RowIndex = PreStartGameRow,
                    ColumnIndex = FirstColumnIndex
                },
                Rows = new List<RowData>
                {
                    new()
                    {
                        Values = new List<CellData>
                        {
                            new() {UserEnteredValue = new ExtendedValue {NumberValue = rowGameNumber}},
                            new() {UserEnteredValue = new ExtendedValue {StringValue = gameName}},
                            new() {UserEnteredValue = new ExtendedValue {StringValue = "0%"}},
                            new() {UserEnteredValue = new ExtendedValue {StringValue = streamNumber.ToString()}}
                        }
                    }
                },
                Fields = "userEnteredValue"
            }
        };
    }

    /// <summary>
    ///     Ищет игру по названию в списке и возвращает её детали.
    /// </summary>
    /// <param name="gameList">Список игр из Google Sheets.</param>
    /// <param name="gameName">Название игры для поиска.</param>
    /// <returns>Детали игры, если она найдена; иначе null.</returns>
    private async Task<string?> FindAndReturnGameDetailsAsync(IList<IList<object>> gameList, string gameName)
    {
        const double minimumSimilarityThreshold = 80.0;
        double highestSimilarity = 0;
        string? mostSimilarGame = null;

        foreach (var row in gameList)
        {
            if (!IsRowValid(row) || row[0].ToString() is not { } foundGameName) continue;
            // Вычисление коэффициента схожести между названиями
            var similarity = GetGameNameSimilarity(foundGameName, gameName);

            // Поиск игры с самым высоким процентом схожести
            if (!(similarity > highestSimilarity)) continue;
            highestSimilarity = similarity;
            mostSimilarGame = foundGameName;
        }

        // Использование константы для проверки порога схожести
        if (highestSimilarity >= minimumSimilarityThreshold && mostSimilarGame != null)
            return await GetGameDetailsAsync(mostSimilarGame).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    ///     Ищет и возвращает детали игры из строк Google Sheets.
    /// </summary>
    /// <param name="rows">Строки из Google Sheets.</param>
    /// <param name="gameName">Название игры для поиска.</param>
    /// <returns>Детали игры, если она найдена; иначе null.</returns>
    private static IList<object>? FindGameDetails(IList<IList<object>> rows, string gameName)
    {
        return rows.FirstOrDefault(row =>
            row[0].ToString()?.Equals(gameName, StringComparison.InvariantCultureIgnoreCase) ?? false);
    }

    /// <summary>
    ///     Ищет строку игры в таблице по названию.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Индекс строки игры или -1, если игра не найдена.</returns>
    private async Task<int> FindGameRowAsync(string gameName)
    {
        var response = await googleSheetsBaseService.GetValuesAsync($"{GameSheetName}!{GameNameColumnLetter}:{GameNameColumnLetter}")
            .ConfigureAwait(false);

        if (response.Values == null) return -1;

        for (var i = 0; i < response.Values?.Count; i++)
            if (response.Values?[i]?.FirstOrDefault()?.ToString()
                    ?.Equals(gameName, StringComparison.InvariantCultureIgnoreCase) ?? false)
                return i + 1;

        return -1;
    }

    /// <summary>
    ///     Форматирует строку с деталями игры для отображения.
    /// </summary>
    /// <param name="row">Строка данных о игре из Google Sheets.</param>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Отформатированная строка с деталями игры.</returns>
    private static string FormatGameDetailsResponse(IList<object> row, string gameName)
    {
        var reply = $"Игра {gameName}. ";
        var streamerRate = row.Count > 3 && !string.IsNullOrEmpty(row[3].ToString()) ? row[3].ToString() : "нет";
        var chatRate = row.Count > 5 && !string.IsNullOrEmpty(row[5].ToString()) ? row[5].ToString() : "нет";
        var comment = row.Count > 4 && !string.IsNullOrEmpty(row[4].ToString()) ? row[4].ToString() : null;

        reply += $"Оценка игры от стримера {streamerRate}, от чата {chatRate}.";
        if (!string.IsNullOrEmpty(comment)) reply += $" Комментарий: {comment}";

        return reply;
    }

    /// <summary>
    ///     Получает существующие номера стримов для указанной строки игры.
    /// </summary>
    /// <param name="row">Индекс строки игры в Google Sheets.</param>
    /// <returns>Строка с существующими номерами стримов.</returns>
    private async Task<string> GetExistingStreamNumbersAsync(int row)
    {
        var range = $"'{GameSheetName}'!{GameStreamNumbersColumnLetter}{row}:{GameStreamNumbersColumnLetter}{row}";
        var response = await googleSheetsBaseService.GetValuesAsync(range).ConfigureAwait(false);
        return response.Values?.FirstOrDefault()?.FirstOrDefault()?.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     Возвращает информацию об игре по её названию.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Подробная информация об игре, если она найдена; иначе null.</returns>
    private async Task<string?> GetGameDetailsAsync(string gameName)
    {
        var response = await GetGameDetailsFromSheetAsync().ConfigureAwait(false);
        if (response?.Values == null || response.Values.Count == 0) return null;

        var gameDetails = FindGameDetails(response.Values, gameName);
        return gameDetails == null ? null : FormatGameDetailsResponse(gameDetails, gameName);
    }

    /// <summary>
    ///     Получает детали игры из листа Google Sheets.
    /// </summary>
    /// <returns>Объект ValueRange, содержащий детали игры.</returns>
    private async Task<ValueRange?> GetGameDetailsFromSheetAsync()
    {
        return await googleSheetsBaseService.GetValuesAsync(
            $"'{GameSheetName}'!{GameNameColumnLetter}{StartGameRow}:{GameChatRateColumnLetter}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Возвращает список всех игр, присутствующих в Google Sheets.
    /// </summary>
    /// <returns>Список игр в виде объекта ValueRange.</returns>
    private async Task<ValueRange> GetGameListAsync()
    {
        return await googleSheetsBaseService.GetValuesAsync($"'{GameSheetName}'!{GameNameColumnLetter}{StartGameRow}:{GameNameColumnLetter}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Получает список значений всех игр из Google Sheets.
    /// </summary>
    /// <returns>Список значений в виде объекта ValueRange.</returns>
    private async Task<IList<IList<object>>> GetGameListValuesAsync()
    {
        var gameList = (await GetGameListAsync().ConfigureAwait(false)).Values;
        return gameList ?? new List<IList<object>>();
    }

    /// <summary>
    ///     Вычисляет процент схожести между двумя названиями игр.
    /// </summary>
    /// <param name="foundGameName">Найденное название игры.</param>
    /// <param name="gameName">Название игры для поиска.</param>
    /// <returns>Процент схожести.</returns>
    private static double GetGameNameSimilarity(string foundGameName, string gameName)
    {
        // Использование FuzzySharp для вычисления процента схожести
        return Fuzz.Ratio(foundGameName, gameName);
    }

    /// <summary>
    ///     Получает следующий доступный номер игры для вставки в таблицу.
    /// </summary>
    /// <returns>Номер следующей игры для вставки.</returns>
    private async Task<int> GetNextGameNumberAsync()
    {
        var response =
            await googleSheetsBaseService.GetValuesAsync($"'{GameSheetName}'!{GameNumberColumnLetter}{StartGameRow}:{GameNumberColumnLetter}")
                .ConfigureAwait(false);
        var rowGameNumber = 1;
        if (response.Values is {Count: > 0} &&
            int.TryParse(response.Values[0][0]?.ToString(), out var currentNumber))
            rowGameNumber = currentNumber + 1;

        return rowGameNumber;
    }

    /// <summary>
    ///     Определяет цвет фона для строки в зависимости от номера игры.
    /// </summary>
    /// <param name="rowGameNumber">Номер игры.</param>
    /// <returns>Цвет для строки игры.</returns>
    private static Color GetRowBackgroundColor(int rowGameNumber)
    {
        var isEvenRow = rowGameNumber % 2 == 0;
        return isEvenRow
            ? EvenRowBackgroundColor
            : OddRowBackgroundColor;
    }

    /// <summary>
    ///     Возвращает обновлённые номера стримов с добавлением нового номера.
    /// </summary>
    /// <param name="existingNumbers">Существующие номера стримов.</param>
    /// <param name="newStreamNumber">Новый номер стрима для добавления.</param>
    /// <returns>Обновлённая строка с номерами стримов.</returns>
    private static string GetUpdatedStreamNumbers(string existingNumbers, int newStreamNumber)
    {
        return string.IsNullOrEmpty(existingNumbers)
            ? newStreamNumber.ToString()
            : $"{existingNumbers}; {newStreamNumber}";
    }

    /// <summary>
    ///     Проверяет, является ли название игры некорректным (пустым или null).
    /// </summary>
    /// <param name="gameName">Название игры для проверки.</param>
    /// <returns>True, если название некорректное; иначе false.</returns>
    private static bool IsGameNameInvalid(string gameName)
    {
        return string.IsNullOrEmpty(gameName);
    }

    /// <summary>
    ///     Проверяет, является ли строка данных о игре валидной.
    /// </summary>
    /// <param name="row">Строка данных о игре.</param>
    /// <returns>True, если строка валидна; иначе false.</returns>
    private static bool IsRowValid(IList<object> row)
    {
        return row is {Count: > 0};
    }

    /// <summary>
    ///     Проверяет, содержит ли строка существующих номеров стримов новый номер.
    /// </summary>
    /// <param name="existingNumbers">Строка с существующими номерами стримов.</param>
    /// <param name="newStreamNumber">Новый номер стрима для проверки.</param>
    /// <returns>True, если номер уже присутствует; иначе false.</returns>
    private static bool IsStreamNumberPresent(string existingNumbers, int newStreamNumber)
    {
        var streamNumbers = existingNumbers.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim());
        return streamNumbers.Contains(newStreamNumber.ToString());
    }

    /// <summary>
    ///     Обновляет номер стрима для указанной игры.
    /// </summary>
    /// <param name="row">Индекс строки игры в таблице.</param>
    /// <param name="newStreamNumber">Новый номер стрима.</param>
    private async Task UpdateStreamNumberAsync(int row, int newStreamNumber)
    {
        var existingNumbers = await GetExistingStreamNumbersAsync(row).ConfigureAwait(false);

        if (IsStreamNumberPresent(existingNumbers, newStreamNumber)) return;

        var updatedNumbers = GetUpdatedStreamNumbers(existingNumbers, newStreamNumber);

        await UpdateStreamNumbersInSheetAsync(row, updatedNumbers).ConfigureAwait(false);
    }

    /// <summary>
    ///     Обновляет номера стримов для указанной строки игры в Google Sheets.
    /// </summary>
    /// <param name="row">Индекс строки игры в таблице.</param>
    /// <param name="updatedNumbers">Обновлённая строка с номерами стримов.</param>
    private async Task UpdateStreamNumbersInSheetAsync(int row, string updatedNumbers)
    {
        var range = $"'{GameSheetName}'!{GameStreamNumbersColumnLetter}{row}:{GameStreamNumbersColumnLetter}{row}";
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> {new List<object> {updatedNumbers}}
        };

        await googleSheetsBaseService.UpdateValuesAsync(range, valueRange).ConfigureAwait(false);
    }
}