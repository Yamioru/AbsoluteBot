using AbsoluteBot.Models;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Классификатор ачивок: какие id засчитываются для текста сообщения.
/// </summary>
public interface IAchievementClassifier
{
    Task<IReadOnlyList<string>> ClassifyAsync(string text, IReadOnlyList<AchievementDefinition> catalog);
}
