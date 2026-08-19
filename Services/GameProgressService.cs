using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

#pragma warning disable IDE0028

/// <summary>
///     Сервис для отслеживания, управления и записью прогресса прохождения игр на стримах.
/// </summary>
public class GameProgressService
    (GameGoogleSheetsService gameGoogleSheetsService, ConfigService configService, HowLongToBeatService howLongToBeatService) : IAsyncInitializable
{
    private static string ProgressFilePath => DataPaths.Get("game_progress.json");
    private const int TimeThresholdForUpdate = 120;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Dictionary<string, int> _accumulatedTimeForUpdate = new();
    private ConcurrentDictionary<string, GameProgress> _gameProgressData = new();
    private string? _lastGameName;

    public async Task InitializeAsync()
    {
        _gameProgressData = await LoadGameProgressDataAsync().ConfigureAwait(false);
        _lastGameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_lastGameName)) Log.Warning("Не удалось загрузить имя последней игры на стриме.");
    }

    /// <summary>
    ///     Добавляет время к текущей игре и обновляет полное время прохождения игры, если накопленное время превышает порог.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="timeToAdd">Добавляемое время в минутах.</param>
    public async Task AddTimeToCurrentGameAsync(string gameName, int timeToAdd)
    {
        try
        {
            // Если прогресс игры уже существует, добавляется время
            if (_gameProgressData.TryGetValue(gameName, out var gameProgress))
            {
                AddTimeToProgress(gameName, timeToAdd, gameProgress);
                await CheckAndUpdateGameTimeAsync(gameName).ConfigureAwait(false);
            }
            else
            {
                // Создание нового прогресса для игры, если его нет
                await CreateNewGameProgressAsync(gameName, timeToAdd).ConfigureAwait(false);
            }

            // Сохранение данных прогресса
            await SaveProgressDataAsync(_gameProgressData).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка добавления времени к текущей игре.");
        }
    }

    /// <summary>
    ///     Рассчитывает текущий прогресс игры в процентах.
    /// </summary>
    /// <returns>Процент прогресса игры или отрицательное значение в случае ошибки.</returns>
    public async Task<double> CalculateGameProgressAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastGameName)) return -1;

            // Получение или создание прогресса для текущий игры
            var gameProgress = await GetOrCreateGameProgressAsync(_lastGameName).ConfigureAwait(false);

            if (gameProgress.TotalPlaytimeMinutes == 0)
            {
                // Если полное время прохождения равно 0, прогресс записывается как бесконечный
                await UpdateGameProgressInGoogleSheetsAsync(-1, true).ConfigureAwait(false);
                return -2;
            }


            var percentage = CalculateProgressPercentage(gameProgress);
            if (percentage < 0) return -3;
            // Прогресс обновляется в Google Sheets
            await UpdateGameProgressInGoogleSheetsAsync(percentage).ConfigureAwait(false);

            return percentage;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка во время вычисления прогресса прохождения игры.");
            return -4;
        }
    }

    /// <summary>
    ///     Генерирует сообщение о текущем прогрессе игры.
    /// </summary>
    /// <returns>Сообщение с информацией о прогрессе игры.</returns>
    public virtual async Task<string> GenerateProgressMessageAsync()
    {
        try
        {
            var progressPercentage = await CalculateGameProgressAsync().ConfigureAwait(false);

            if (_lastGameName == null) return "Не найдена игра на стриме.";

            if (progressPercentage < 0 || _gameProgressData[_lastGameName].TotalPlaytimeMinutes == 0)
                return
                    $"Прогресс игры '{_lastGameName}' не может быть рассчитан, так как полное время прохождения неизвестно. Часов проведено в игре: {_gameProgressData[_lastGameName].CurrentPlaytimeMinutes / 60}.";

            return $"Игра '{_lastGameName}' пройдена на {progressPercentage}%";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении строки прогресса игры.");
            return "Не получилось узнать прогресс игры.";
        }
    }

    /// <summary>
    ///     Асинхронно обновляет название последней игры.
    /// </summary>
    /// <param name="lastGameName">Новое название игры.</param>
    public async Task UpdateLastGameNameAsync(string lastGameName)
    {
        try
        {
            if (lastGameName != _lastGameName)
            {
                _lastGameName = lastGameName;
                await configService.SetConfigValueAsync("LastGameName", _lastGameName).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении названия последней игры.");
        }
    }

    /// <summary>
    ///     Добавляет время к прогрессу игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="timeToAdd">Добавляемое время.</param>
    /// <param name="gameProgress">Объект прогресса игры.</param>
    private void AddTimeToProgress(string gameName, int timeToAdd, GameProgress gameProgress)
    {
        gameProgress.CurrentPlaytimeMinutes += timeToAdd;

        if (_accumulatedTimeForUpdate.ContainsKey(gameName))
            _accumulatedTimeForUpdate[gameName] += timeToAdd;
        else
            _accumulatedTimeForUpdate[gameName] = timeToAdd;
    }

    /// <summary>
    ///     Рассчитывает процент прохождения игры.
    /// </summary>
    /// <param name="gameProgress">Объект прогресса игры.</param>
    /// <returns>Процент прогресса игры.</returns>
    private static double CalculateProgressPercentage(GameProgress gameProgress)
    {
        if (gameProgress.TotalPlaytimeMinutes <= 0) return -1;
        var percentage = (double) gameProgress.CurrentPlaytimeMinutes / gameProgress.TotalPlaytimeMinutes * 100;
        return Math.Round(percentage, 2);
    }

    /// <summary>
    ///     Проверяет накопленное время и обновляет общее время прохождения игры, если накопленное время превышает порог.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    private async Task CheckAndUpdateGameTimeAsync(string gameName)
    {
        if (_accumulatedTimeForUpdate[gameName] >= TimeThresholdForUpdate)
        {
            await UpdateFullTimeAsync(gameName).ConfigureAwait(false);
            _accumulatedTimeForUpdate[gameName] = 0;
        }
    }

    /// <summary>
    ///     Создает новый объект прогресса игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="timeToAdd">Добавляемое время.</param>
    private async Task CreateNewGameProgressAsync(string gameName, int timeToAdd)
    {
        var gameProgress = new GameProgress
        {
            GameName = gameName,
            CurrentPlaytimeMinutes = timeToAdd,
            TotalPlaytimeMinutes = await howLongToBeatService.GetGameDurationAsync(gameName).ConfigureAwait(false),
            LastLoggedPercentage = 0
        };
        _gameProgressData[gameName] = gameProgress;
    }

    /// <summary>
    ///     Получает или создает объект прогресса игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Объект прогресса игры.</returns>
    private async Task<GameProgress> GetOrCreateGameProgressAsync(string gameName)
    {
        var gameProgress = _gameProgressData.Values.FirstOrDefault(
            g => g.GameName.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));

        if (gameProgress != null) return gameProgress;
        gameProgress = new GameProgress
        {
            GameName = gameName,
            TotalPlaytimeMinutes = await howLongToBeatService.GetGameDurationAsync(gameName).ConfigureAwait(false),
            CurrentPlaytimeMinutes = 0,
            LastLoggedPercentage = 0
        };
        _gameProgressData.TryAdd(gameName, gameProgress);
        await SaveProgressDataAsync(_gameProgressData).ConfigureAwait(false);

        return gameProgress;
    }

    /// <summary>
    ///     Асинхронно загружает данные о прогрессе игр из файла, если файл существует.
    ///     Если файл отсутствует, создается новый словарь прогресса игр.
    /// </summary>
    /// <returns>Словарь данных прогресса игр.</returns>
    private static async Task<ConcurrentDictionary<string, GameProgress>> LoadGameProgressDataAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(ProgressFilePath)) return new ConcurrentDictionary<string, GameProgress>();
            var jsonData = await File.ReadAllTextAsync(ProgressFilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, GameProgress>>(jsonData)
                   ?? new ConcurrentDictionary<string, GameProgress>();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет данные о прогрессе игр в файл.
    /// </summary>
    /// <param name="gameProgressData"> Данные о прогрессах игр.</param>
    private static async Task SaveProgressDataAsync(ConcurrentDictionary<string, GameProgress> gameProgressData)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(gameProgressData, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, ProgressFilePath, true)).ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Обновляет полное время прохождения игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    private async Task UpdateFullTimeAsync(string gameName)
    {
        try
        {
            var newFullTime = await howLongToBeatService.GetGameDurationAsync(gameName).ConfigureAwait(false);
            if (newFullTime > 0)
            {
                _gameProgressData[gameName].TotalPlaytimeMinutes = newFullTime;
                await SaveProgressDataAsync(_gameProgressData).ConfigureAwait(false);
            }
            else if (_gameProgressData[gameName].TotalPlaytimeMinutes != 0)
            {
                Log.Warning(
                    $"Не удалось обновить TotalPlaytimeMinutes для игры '{gameName}'. Оставляется старое значение.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при обновлении TotalPlaytimeMinutes для игры '{gameName}'.");
        }
    }

    /// <summary>
    ///     Обновляет прогресс игры в таблице Google Sheets, если целочисленный процент прогресса увеличился.
    ///     Если полное время прохождения игры равно 0, отправляет текущее время в игре.
    /// </summary>
    /// <param name="progressPercentage">Процент прогресса игры.</param>
    /// <param name="isInfinite">Указывает, является ли прогресс бесконечным (полное время прохождения равно 0).</param>
    private async Task UpdateGameProgressInGoogleSheetsAsync(double progressPercentage, bool isInfinite = false)
    {
        if (_lastGameName == null) return;
        if (_gameProgressData.TryGetValue(_lastGameName, out var gameProgress))
        {
            if (isInfinite)
                await WriteCurrentGameTimeAsync(gameProgress).ConfigureAwait(false);
            else if (progressPercentage >= 0) await WriteProgressIfIncreasedAsync(gameProgress, progressPercentage).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Записывает текущее время игры в Google Sheets.
    /// </summary>
    /// <param name="gameProgress">Объект прогресса игры.</param>
    private async Task WriteCurrentGameTimeAsync(GameProgress gameProgress)
    {
        if (_lastGameName == null) return;
        await gameGoogleSheetsService.UpdateGameProgressAsync(_lastGameName, $"{gameProgress.CurrentPlaytimeMinutes / 60} ч.").ConfigureAwait(false);
        await SaveProgressDataAsync(_gameProgressData).ConfigureAwait(false);
    }

    /// <summary>
    ///     Записывает прогресс в Google Sheets, если процент прогресса увеличился.
    /// </summary>
    /// <param name="gameProgress">Объект прогресса игры.</param>
    /// <param name="progressPercentage">Процент прогресса игры.</param>
    private async Task WriteProgressIfIncreasedAsync(GameProgress gameProgress, double progressPercentage)
    {
        if (_lastGameName == null) return;
        var roundedProgress = (int) Math.Floor(progressPercentage);
        if (roundedProgress > gameProgress.LastLoggedPercentage)
        {
            gameProgress.LastLoggedPercentage = roundedProgress;
            await SaveProgressDataAsync(_gameProgressData).ConfigureAwait(false);
            await gameGoogleSheetsService.UpdateGameProgressAsync(_lastGameName, $"{roundedProgress}%").ConfigureAwait(false);
        }
    }
}