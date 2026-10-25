namespace AbsoluteBot.Services.MediaServices;

/// <summary>
///     Интерфейс для сервиса генерации изображений на основе текста.
/// </summary>
public interface IImageGeneratorService
{
    /// <summary>
    ///     Генерирует изображение на основе переданного текста команд и загружает его на внешний сервер.
    /// </summary>
    /// <param name="commandsText">Текст, который будет преобразован в изображение.</param>
    /// <returns>URL загруженного изображения или null в случае ошибки.</returns>
    Task<string?> GenerateCommandsImageAsync(string commandsText);
}