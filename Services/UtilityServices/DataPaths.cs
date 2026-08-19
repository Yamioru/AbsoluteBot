namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Разрешает пути к файлам данных относительно корня данных.
///     Корень: переменная окружения ABSOLUTEBOT_DATA_ROOT (если задана и существует), иначе текущая директория.
/// </summary>
public static class DataPaths
{
    public const string DataRootEnvironmentVariable = "ABSOLUTEBOT_DATA_ROOT";

    /// <summary>
    ///     Корневая директория данных бота.
    /// </summary>
    public static string Root
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
                return Path.GetFullPath(configured);
            return Directory.GetCurrentDirectory();
        }
    }

    /// <summary>
    ///     Возвращает абсолютный путь к файлу относительно корня данных.
    /// </summary>
    public static string Get(string relativeFileName)
    {
        if (string.IsNullOrWhiteSpace(relativeFileName))
            throw new ArgumentException("Имя файла не задано.", nameof(relativeFileName));
        if (Path.IsPathRooted(relativeFileName))
            return relativeFileName;
        return Path.Combine(Root, relativeFileName);
    }
}
