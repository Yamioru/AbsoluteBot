using AbsoluteBot.Services.UtilityServices;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Serilog;

namespace AbsoluteBot.Services.GoogleSheetsServices;

/// <summary>
///     Базовый класс для работы с Google Sheets API.
///     Реализует основные методы для взаимодействия с таблицами, включая получение значений, обновление значений, и
///     выполнение пакетных запросов.
/// </summary>
public class GoogleSheetsBaseService(ConfigService configService) : IAsyncInitializable
{
    private static string CredentialsFilePath => DataPaths.Get("google_sheets_credentials.json");
    private const int SheetNotFound = -1;

    private const SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum ValueRenderOptionFormula =
        SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMULA;

    private const SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum ValueInputOptionUserEntered =
        SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

    public SheetsService? SheetsService;
    public string? SheetsId;

    public async Task InitializeAsync()
    {
        try
        {
            SheetsId = await configService.GetConfigValueAsync<string>("GoogleSheetsId").ConfigureAwait(false);
            if (!File.Exists(CredentialsFilePath))
            {
                Log.Warning("Не удалось загрузить данные для подключения к google sheets.");
                return;
            }

            var appName = await configService.GetConfigValueAsync<string>("GoogleAppName").ConfigureAwait(false);
            GoogleCredential credential;
            await using (var stream = new FileStream(CredentialsFilePath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            SheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = appName
            });
            if (string.IsNullOrEmpty(SheetsId) || string.IsNullOrEmpty(appName)) Log.Warning("Не удалось загрузить данные для подключения к google sheets.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при подключении сервиса Google Sheets.");
        }
    }

    /// <summary>
    ///     Выполняет пакетный запрос на обновление данных в таблице.
    /// </summary>
    /// <param name="requests">Список запросов для выполнения.</param>
    public async Task ExecuteBatchUpdateAsync(IList<Request> requests)
    {
        if (SheetsService == null)
            throw new InvalidOperationException("SheetsService is not initialized");

        var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = requests
        };

        var batchUpdate = SheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, SheetsId);
        await batchUpdate.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Получает значения ячеек с возможностью указать тип данных для возврата.
    ///     В этом случае возвращается формула ячейки.
    /// </summary>
    /// <param name="range">Диапазон ячеек для получения данных.</param>
    /// <returns>Значения ячеек, включая формулу, если она существует.</returns>
    public async Task<ValueRange> GetFormulaValuesAsync(string range)
    {
        if (SheetsService == null)
            throw new InvalidOperationException("SheetsService is not initialized");

        var request = SheetsService.Spreadsheets.Values.Get(SheetsId, range);
        request.ValueRenderOption = ValueRenderOptionFormula;
        return await request.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно получает идентификатор листа по его названию.
    /// </summary>
    /// <param name="sheetName">Название листа в Google Sheets.</param>
    /// <returns>Идентификатор листа или -1, если лист не найден.</returns>
    public async Task<int> GetSheetIdByNameAsync(string sheetName)
    {
        if (SheetsService == null)
            throw new InvalidOperationException("SheetsService is not initialized");

        // Получение информации о таблице
        var spreadsheet = await SheetsService.Spreadsheets.Get(SheetsId).ExecuteAsync().ConfigureAwait(false);
        // Поиск листа по его названию
        var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);

        // Если лист не найден, возвращается SheetNotFound
        return sheet?.Properties?.SheetId ?? SheetNotFound;
    }

    /// <summary>
    ///     Получает значения из указанного диапазона в таблице.
    /// </summary>
    /// <param name="range">Диапазон в формате A1 (например, "Лист1!A1:B2").</param>
    /// <returns>Объект ValueRange с данными из указанного диапазона.</returns>
    public async Task<ValueRange> GetValuesAsync(string range)
    {
        if (SheetsService == null)
            throw new InvalidOperationException("SheetsService is not initialized");

        var request = SheetsService.Spreadsheets.Values.Get(SheetsId, range);
        return await request.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Обновляет значения в указанном диапазоне в таблице.
    /// </summary>
    /// <param name="range">Диапазон в формате A1 (например, "Лист1!A1:B2").</param>
    /// <param name="body">Объект ValueRange с новыми данными для обновления.</param>
    public async Task UpdateValuesAsync(string range, ValueRange body)
    {
        if (SheetsService == null)
            throw new InvalidOperationException("SheetsService is not initialized");

        var updateRequest = SheetsService.Spreadsheets.Values.Update(body, SheetsId, range);
        updateRequest.ValueInputOption = ValueInputOptionUserEntered;
        await updateRequest.ExecuteAsync().ConfigureAwait(false);
    }
}