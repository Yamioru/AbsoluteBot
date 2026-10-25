namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Тип канала/чата/гильдии где может писать бот
/// </summary>
public enum ChannelType
{
    /// <summary>
    ///     Стандартный тип, присваивается для мест не связанных с основными чатами стрима.
    /// </summary>
    General,

    /// <summary>
    ///     Особый тип, присваивается для мест связанных с основными чатами стрима.
    /// </summary>
    Premium,

    /// <summary>
    ///     Административный тип для чатов управления ботом.
    /// </summary>
    Administrative,

    /// <summary>
    ///     Тип используемый для чатов, в которых публикуются анонсы стримов.
    /// </summary>
    Announce
}