namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Идентификаторы провайдеров текстовой нейросети.
/// </summary>
public static class NeuralProviders
{
    public const string Gemini = "gemini";
    public const string Groq = "groq";
}

/// <summary>
///     Сущности, для которых задаётся отдельная модель.
/// </summary>
public static class NeuralEntities
{
    public const string Chat = "Chat";
    public const string Ask = "Ask";
    public const string GoogleSearch = "GoogleSearch";
    public const string Image = "Image";

    public static readonly string[] All = {Chat, Ask, GoogleSearch, Image};
}
