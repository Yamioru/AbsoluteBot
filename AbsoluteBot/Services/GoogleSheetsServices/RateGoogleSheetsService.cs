using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using AbsoluteBot.Services.UtilityServices;
using Google.Apis.Sheets.v4.Data;
using Serilog;

namespace AbsoluteBot.Services.GoogleSheetsServices;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для работы с оценками игр в Google Sheets.
/// </summary>
public partial class RateGoogleSheetsService(GoogleSheetsBaseService googleSheetsBaseService) : IAsyncInitializable
{
    private static string RateFilePath => DataPaths.Get("gamesRates.json");
    private const string GameSheetName = "Игры"; // Название листа со списком игр
    private const string GameNameColumnLetter = "B"; // Столбец названий игр
    private const string GameChatRateColumnLetter = "G"; // Столбец оценки чата для игр
    private const int StartGameRow = 13; // Строка с которой начинается список игр
    private const int ScoreGameColumn = 6; // Столбец оценки чата для игр
    private const int PreStartGameRow = StartGameRow - 1; // Строка после которой начинается список игр

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _gamesRate = new();

    public async Task InitializeAsync()
    {
        try
        {
            _gamesRate = await LoadGamesRateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при инициализации RateGoogleSheetsService.");
        }
    }

    /// <summary>
    ///     Асинхронно изменяет оценку игры для указанного пользователя.
    /// </summary>
    /// <param name="value">Новое значение оценки, ожидается от 0 до 10.</param>
    /// <param name="username">Имя пользователя, который ставит оценку.</param>
    /// <param name="gameName">Название игры, для которой меняется оценка.</param>
    /// <returns>Сообщение о результате выполнения операции.</returns>
    public virtual async Task<string> UpdateGameRatingAsync(string value, string username, string gameName)
    {
        try
        {
            if (googleSheetsBaseService.SheetsService == null)
            {
                Log.Warning("Google Sheets Service не работает и равен null.");
                return "Не удалось подключиться к таблице.";
            }

            // Валидация названия игры
            var validGameName = ValidateGameName(gameName);
            if (validGameName == null)
                return "Не удалось определить название игры.";

            // Поиск индекса игры
            var gameIndex = await GetGameIndexAsync(validGameName).ConfigureAwait(false);
            if (gameIndex == -1)
                return $"Игра \"{validGameName}\" не найдена.";

            // Валидация рейтинга
            var intValue = ValidateRating(value);
            if (intValue == null)
                return "Нужно ввести число от 0 до 10.";

            // Получение обновленной формулы
            var formula = await CalculateNewFormulaAsync(gameIndex, intValue.Value, validGameName, username).ConfigureAwait(false);
            if (formula == null)
                return "Ошибка при обновлении формулы.";

            // Применение формулы в таблице
            await ApplyFormulaToSheetAsync(gameIndex, formula).ConfigureAwait(false);
            await SaveGamesRateAsync(_gamesRate).ConfigureAwait(false);
            return $"Оценка для игры \"{validGameName}\" обновлена.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при изменении оценки игры.");
            return "Ошибка при обновлении формулы.";
        }
    }

    /// <summary>
    ///     Применяет формулу обновления оценки в Google Sheets.
    /// </summary>
    /// <param name="gameIndex">Индекс игры в таблице.</param>
    /// <param name="formula">Формула для обновления оценки.</param>
    private async Task ApplyFormulaToSheetAsync(int gameIndex, string formula)
    {
        await FillTableWithFormulaAsync(formula, gameIndex + PreStartGameRow, ScoreGameColumn,
            await googleSheetsBaseService.GetSheetIdByNameAsync(GameSheetName).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Получает обновленную формулу для оценки игры на основе существующих данных.
    /// </summary>
    /// <param name="gameIndex">Индекс игры в таблице.</param>
    /// <param name="newRating">Новая оценка для обновления.</param>
    /// <param name="gameName">Название игры.</param>
    /// <param name="username">Имя пользователя, который ставит оценку.</param>
    /// <returns>Обновленная формула или null, если что-то пошло не так.</returns>
    private async Task<string?> CalculateNewFormulaAsync(int gameIndex, int newRating, string gameName, string username)
    {
        // Получение существующей формулы для указанной игры
        var response = await googleSheetsBaseService.GetFormulaValuesAsync(
                $"'{GameSheetName}'!{GameChatRateColumnLetter}{gameIndex + StartGameRow}:{GameChatRateColumnLetter}{gameIndex + StartGameRow}")
            .ConfigureAwait(false);

        // Проверка, существует ли уже формула в таблице

        // Если формула существует, она обновляется, иначе возвращается null
        return response.Values?.FirstOrDefault()?.FirstOrDefault() is not string existFormula
            ? CreateNewRatingFormula(newRating, gameName, username)
            : UpdateGameRatingFormula(existFormula, newRating, gameName, username);
    }

    /// <summary>
    ///     Создаёт новую формулу для расчета оценки игры.
    /// </summary>
    /// <param name="newRating">Первая оценка, которая будет добавлена.</param>
    /// <param name="gameName">Название игры, для которой создаётся формула.</param>
    /// <param name="username">Имя пользователя, который ставит оценку.</param>
    /// <returns>Новая формула для использования в таблице.</returns>
    private string CreateNewRatingFormula(int newRating, string gameName, string username)
    {
        // Добавление новой игры и её оценки в локальный словарь оценок
        if (!_gamesRate.TryGetValue(gameName, out var userRatings))
        {
            // Создание нового ConcurrentDictionary для игры и её оценок
            var newUserRatings = new ConcurrentDictionary<string, int>();
            newUserRatings.TryAdd(username, newRating);
            _gamesRate.TryAdd(gameName, newUserRatings);
        }
        else
        {
            userRatings[username] = newRating; // Обновление оценки для пользователя
        }

        // Формула для первой оценки
        return $"=ROUND(AVERAGE({newRating});1)";
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex DigitsRegex();

    /// <summary>
    ///     Применяет формулу в указанную ячейку Google Sheets.
    /// </summary>
    /// <param name="formula">Формула для добавления в таблицу.</param>
    /// <param name="row">Номер строки, в которую будет добавлена формула.</param>
    /// <param name="column">Номер колонки для добавления формулы.</param>
    /// <param name="sheetId">Идентификатор листа Google Sheets.</param>
    private async Task FillTableWithFormulaAsync(string formula, int row, int column, int sheetId)
    {
        var requests = new List<Request>
        {
            new()
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Start = new GridCoordinate
                    {
                        SheetId = sheetId,
                        RowIndex = row,
                        ColumnIndex = column
                    },
                    Rows = new List<RowData>
                    {
                        new()
                        {
                            Values = new List<CellData>
                            {
                                new()
                                {
                                    UserEnteredValue = new ExtendedValue
                                    {
                                        FormulaValue = formula
                                    }
                                }
                            }
                        }
                    },
                    Fields = "userEnteredValue"
                }
            }
        };
        await googleSheetsBaseService.ExecuteBatchUpdateAsync(requests).ConfigureAwait(false);
    }

    /// <summary>
    ///     Находит индекс игры по названию в списке игр.
    /// </summary>
    /// <param name="gameList">Список игр из Google Sheets.</param>
    /// <param name="gameName">Название игры для поиска.</param>
    /// <returns>Индекс игры или -1, если игра не найдена.</returns>
    private static int FindGameIndex(IList<IList<object>> gameList, string gameName)
    {
        for (var i = 0; i < gameList.Count; i++)
            if (gameList[i][0].ToString()?.Equals(gameName, StringComparison.InvariantCultureIgnoreCase) == true)
                return i;
        return -1;
    }

    /// <summary>
    ///     Асинхронно находит индекс игры в Google Sheets.
    /// </summary>
    /// <param name="gameName">Название игры для поиска в таблице.</param>
    /// <returns>Индекс игры или -1, если игра не найдена.</returns>
    private async Task<int> GetGameIndexAsync(string gameName)
    {
        var gameList = (await GetGameListAsync().ConfigureAwait(false)).Values;
        return FindGameIndex(gameList, gameName);
    }

    /// <summary>
    ///     Загружает список игр из Google Sheets.
    /// </summary>
    /// <returns>Список значений для игры.</returns>
    private async Task<ValueRange> GetGameListAsync()
    {
        return await googleSheetsBaseService.GetValuesAsync($"'{GameSheetName}'!{GameNameColumnLetter}{StartGameRow}:{GameNameColumnLetter}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно загружает ранее сохраненные оценки игр из файла.
    /// </summary>
    /// <returns>Словарь с оценками игр.</returns>
    private static async Task<ConcurrentDictionary<string, ConcurrentDictionary<string, int>>> LoadGamesRateAsync()
    {
        if (!File.Exists(RateFilePath)) return new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();
        var json = await File.ReadAllTextAsync(RateFilePath).ConfigureAwait(false);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json)
                           ?? new Dictionary<string, Dictionary<string, int>>();
        return new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>(
            deserialized.ToDictionary(
                kvp => kvp.Key,
                kvp => new ConcurrentDictionary<string, int>(kvp.Value)));
    }

    /// <summary>
    ///     Асинхронно сохраняет текущие оценки игр в файл.
    /// </summary>
    /// <param name="gamesRate">Словарь с оценками для сохранения.</param>
    private static async Task SaveGamesRateAsync(ConcurrentDictionary<string, ConcurrentDictionary<string, int>> gamesRate)
    {
        var json = JsonSerializer.Serialize(gamesRate, JsonOptions);
        await File.WriteAllTextAsync(RateFilePath, json).ConfigureAwait(false);
    }

    /// <summary>
    ///     Обновляет формулу оценки игры в зависимости от новых данных.
    /// </summary>
    /// <param name="existingFormula">Текущая формула в таблице.</param>
    /// <param name="newRating">Новая оценка для обновления.</param>
    /// <param name="gameName">Название игры, для которой обновляется оценка.</param>
    /// <param name="username">Имя пользователя, оставившего оценку.</param>
    /// <returns>Обновленная формула для использования в таблице.</returns>
    private string UpdateGameRatingFormula(string existingFormula, int newRating, string gameName,
        string username)
    {
        if (!_gamesRate.TryGetValue(gameName, out var userRatings))
        {
            var newUserRatings = new ConcurrentDictionary<string, int>();
            newUserRatings.TryAdd(username, newRating);
            _gamesRate.TryAdd(gameName, newUserRatings);
            return $"=ROUND(AVERAGE({newRating});1)";
        }

        if (!userRatings.TryGetValue(username, out var oldRating))
        {
            userRatings.TryAdd(username, newRating);
            return existingFormula.Replace(");1)", $";{newRating});1)");
        }

        var regex = new Regex($@"\b{oldRating}\b"); // Используем границы слова для точной замены только старого рейтинга
        existingFormula = regex.Replace(existingFormula, newRating.ToString(), 1);
        userRatings[username] = newRating;

        return existingFormula;
    }

    /// <summary>
    ///     Валидирует и очищает введённое название игры.
    /// </summary>
    /// <param name="gameName">Название игры для валидации.</param>
    /// <returns>Очищенное название игры или null, если оно некорректно.</returns>
    private static string? ValidateGameName(string gameName)
    {
        gameName = Regex.Unescape(gameName);
        return string.IsNullOrEmpty(gameName) ? null : gameName;
    }

    /// <summary>
    ///     Валидирует введённое числовое значение оценки.
    /// </summary>
    /// <param name="value">Строка с вводом для преобразования в число.</param>
    /// <returns>Целое число (оценка) или null, если введено некорректное значение.</returns>
    private static int? ValidateRating(string value)
    {
        value = DigitsRegex().Replace(value, "");
        if (int.TryParse(value, out var intValue) && intValue is >= 0 and <= 10)
            return intValue;
        return null;
    }
}