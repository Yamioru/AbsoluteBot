namespace AbsoluteBot.Models;

/// <summary>
///     Представляет информацию о сообщении, на которое был дан ответ пользователем.
/// </summary>
/// <param name="username">Имя пользователя, отправившего исходное сообщение, на которое был дан ответ пользователем.</param>
/// <param name="message">Текст исходного сообщения, на которое был дан ответ пользователем.</param>
public class ReplyInfo(string username, string message)
{
    /// <summary>
    ///     Текст сообщения, на которое был дан ответ пользователем.
    /// </summary>
    public string Message { get; set; } = message;
    /// <summary>
    ///     Имя пользователя, отправившего сообщение, на которое был дан ответ пользователем.
    /// </summary>
    public string Username { get; set; } = username;
}