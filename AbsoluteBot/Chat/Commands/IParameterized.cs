namespace AbsoluteBot.Chat.Commands;

/// <summary>
///     Интерфейс у команды характеризующий наличие параметров у команды.
/// </summary>
public interface IParameterized
{
    /// <summary>
    ///     Описание контента который содержат параметры.
    /// </summary>
    string Parameters { get; }
}