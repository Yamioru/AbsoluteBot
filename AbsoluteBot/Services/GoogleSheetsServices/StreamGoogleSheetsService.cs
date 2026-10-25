using Google.Apis.Sheets.v4.Data;
using Serilog;

namespace AbsoluteBot.Services.GoogleSheetsServices;
#pragma warning disable IDE0028
/// <summary>
///     Сервис для работы с таблицей Google Sheets, которая содержит информацию о стримах.
/// </summary>
public class StreamGoogleSheetsService(GoogleSheetsBaseService googleSheetsBaseService)
{
    private const string StreamSheetName = "Стримы"; // Название листа со списком стримов
    private const string StreamGamesNameColumnLetter = "E"; // Столбец названий игр на стриме
    private const string StreamDateColumnLetter = "C"; // Столбец дат стримов
    private const int StreamDateColumnIndex = 2; // Столбец дат стримов
    private const int EndStreamRow = 4; // Строка с которой начинается список стримов
    private const int StartStreamRow = EndStreamRow - 1; // Строка после которой начинается список стримов
    private const string DateFormat = "dd.MM.yyyy";

    /// <summary>
    ///     Добавляет строку для нового стрима или обновляет данные о стриме, если он уже существует.
    /// </summary>
    /// <param name="streamNumber">Номер стрима.</param>
    /// <param name="gameName">Название игры, связанной со стримом.</param>
    /// <returns>Задача асинхронного выполнения.</returns>
    public async Task AddOrUpdateStreamRowAsync(int streamNumber, string gameName)
    {
        try
        {
            if (googleSheetsBaseService.SheetsService == null)
            {
                Log.Warning("Google Sheets Service не работает и равен null.");
                return;
            }

            // Проверка, существует ли стрим в таблице
            var (exists, row) = await CheckStreamExistenceAsync(streamNumber).ConfigureAwait(false);
            if (exists)
                // Обновление информации о стриме, если он существует
                await UpdateStreamGameListAsync(row, gameName).ConfigureAwait(false);
            else
                // Добавление нового стрима, если он не найден
                await AddStreamRowWithDetailsAsync(streamNumber, gameName, DateTime.Now.ToString(DateFormat)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении или обновлении стрима в Google Sheets.");
        }
    }

    /// <summary>
    ///     Вставляет новую строку для стрима в таблицу Google Sheets.
    /// </summary>
    /// <param name="streamNumber">Номер нового стрима.</param>
    /// <param name="gameName">Название игры, связанной с новым стримом.</param>
    /// <param name="currentDate">Дата проведения стрима.</param>
    /// <returns>Задача асинхронного выполнения.</returns>
    private async Task AddStreamRowWithDetailsAsync(int streamNumber, string gameName, string currentDate)
    {
        var sheetId = await googleSheetsBaseService.GetSheetIdByNameAsync(StreamSheetName).ConfigureAwait(false);
        if (sheetId < 0)
        {
            Log.Warning("Не удалось найти лист по его названию");
            return;
        }

        var requests = PrepareInsertStreamRowRequests(sheetId, streamNumber, gameName, currentDate);
        await googleSheetsBaseService.ExecuteBatchUpdateAsync(requests).ConfigureAwait(false);
    }

    /// <summary>
    ///     Проверяет наличие стрима с указанным номером в таблице Google Sheets.
    /// </summary>
    /// <param name="streamNumber">Номер стрима для поиска.</param>
    /// <returns>Кортеж с результатом наличия стрима и номером строки, если стрим существует.</returns>
    private async Task<(bool exists, int row)> CheckStreamExistenceAsync(int streamNumber)
    {
        var response = await googleSheetsBaseService.GetValuesAsync(
            $"'{StreamSheetName}'!{StreamDateColumnLetter}{EndStreamRow}:{StreamGamesNameColumnLetter}").ConfigureAwait(false);
        for (var i = 0; i < response.Values.Count; i++)
        {
            var row = response.Values[i];
            if (row.Count > 1 && row[1].ToString() == streamNumber.ToString()) return (true, i + EndStreamRow);
        }

        return (false, 0);
    }

    /// <summary>
    ///     Проверяет, присутствует ли игра в списке существующих игр.
    /// </summary>
    /// <param name="existingGames">Список существующих игр, разделенный точкой с запятой.</param>
    /// <param name="newGameName">Название новой игры, которую необходимо проверить.</param>
    /// <returns>
    ///     Возвращает <c>true</c>, если игра уже находится в списке, и <c>false</c>, если игра отсутствует.
    /// </returns>
    private static bool IsGameAlreadyInList(string existingGames, string newGameName)
    {
        return existingGames.Split(';').Contains(newGameName);
    }

    /// <summary>
    ///     Подготавливает список запросов для вставки новой строки стрима в таблицу Google Sheets.
    /// </summary>
    /// <param name="sheetId">Идентификатор листа, в который будет вставлена строка.</param>
    /// <param name="streamNumber">Номер стрима для вставки в новую строку.</param>
    /// <param name="gameName">Название игры, связанной с новым стримом.</param>
    /// <param name="currentDate">Дата проведения стрима, которая будет вставлена в таблицу.</param>
    /// <returns>Список запросов, которые будут выполнены для вставки новой строки и обновления ячеек.</returns>
    private static List<Request> PrepareInsertStreamRowRequests(int sheetId, int streamNumber, string gameName,
        string currentDate)
    {
        return new List<Request>
        {
            new()
            {
                InsertDimension = new InsertDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = sheetId,
                        Dimension = "ROWS",
                        StartIndex = StartStreamRow,
                        EndIndex = EndStreamRow
                    },
                    InheritFromBefore = false
                }
            },
            new()
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Start = new GridCoordinate
                    {
                        SheetId = sheetId,
                        RowIndex = StartStreamRow,
                        ColumnIndex = StreamDateColumnIndex
                    },
                    Rows = new List<RowData>
                    {
                        new()
                        {
                            Values = new List<CellData>
                            {
                                new() {UserEnteredValue = new ExtendedValue {StringValue = currentDate}},
                                new() {UserEnteredValue = new ExtendedValue {StringValue = streamNumber.ToString()}},
                                new() {UserEnteredValue = new ExtendedValue {StringValue = gameName}}
                            }
                        }
                    },
                    Fields = "userEnteredValue"
                }
            }
        };
    }

    /// <summary>
    ///     Подготавливает обновленный список игр, добавляя новую игру, если она отсутствует.
    /// </summary>
    /// <param name="existingGames">Список существующих игр, разделенный точкой с запятой.</param>
    /// <param name="newGameName">Название новой игры, которая будет добавлена в список.</param>
    /// <returns>
    ///     Возвращает обновленный список игр в формате строки, где игры разделены точкой с запятой.
    ///     Если список игр пуст, то возвращает только название новой игры.
    /// </returns>
    private static string PrepareUpdatedGameList(string existingGames, string newGameName)
    {
        return string.IsNullOrEmpty(existingGames)
            ? newGameName
            : $"{existingGames}; {newGameName}";
    }

    /// <summary>
    ///     Обновляет список игр для существующего стрима в таблице Google Sheets.
    /// </summary>
    /// <param name="row">Номер строки, где расположен стрим.</param>
    /// <param name="newGameName">Название новой игры для добавления в стрим.</param>
    /// <returns>Задача асинхронного выполнения.</returns>
    private async Task UpdateStreamGameListAsync(int row, string newGameName)
    {
        var range = $"'{StreamSheetName}'!{StreamGamesNameColumnLetter}{row}:{StreamGamesNameColumnLetter}{row}";
        var response = await googleSheetsBaseService.GetValuesAsync(range).ConfigureAwait(false);

        var existingGames = response.Values?[0]?[0]?.ToString() ?? string.Empty;

        // Проверка, есть ли игра в списке, и добавление, если её нет
        if (!IsGameAlreadyInList(existingGames, newGameName))
        {
            var updatedGames = PrepareUpdatedGameList(existingGames, newGameName);

            // Подготовка данных для обновления
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> {new List<object> {updatedGames}}
            };

            // Обновление ячейки с новым значением
            await googleSheetsBaseService.UpdateValuesAsync(range, valueRange).ConfigureAwait(false);
        }
    }
}