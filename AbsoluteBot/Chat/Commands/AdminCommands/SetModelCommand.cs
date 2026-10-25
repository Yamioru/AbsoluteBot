using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Задаёт модель для сущности (Chat, Ask, GoogleSearch, Image).
/// </summary>
public class SetModelCommand(NeuralModelConfigService modelConfig) : BaseCommand, IParameterized
{
    public override int Priority => 708;
    public override string Description => "задаёт модель нейросети для сущности.";
    public override string Name => "!setmodel";
    public string Parameters => "сущность [gemini|groq] id-модели";

    public override bool CanExecute(ParsedCommand command) =>
        CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parts = command.Parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return $"Использование: {Name} Chat|Ask|GoogleSearch|Image [gemini|groq] id-модели";

        var entity = NeuralModelConfigService.NormalizeEntity(parts[0]);
        if (entity == null)
            return "Неизвестная сущность. Доступны: Chat, Ask, GoogleSearch, Image.";

        string? provider = null;
        string modelId;
        if (parts.Length >= 3 && NeuralModelConfigService.NormalizeProvider(parts[1]) != null)
        {
            provider = parts[1];
            modelId = string.Join(' ', parts.Skip(2));
        }
        else
        {
            modelId = string.Join(' ', parts.Skip(1));
        }

        if (!await modelConfig.SetModelAsync(entity, modelId, provider).ConfigureAwait(false))
            return "Не удалось сохранить модель.";

        var targetProvider = entity == NeuralEntities.Image
            ? NeuralProviders.Gemini
            : NeuralModelConfigService.NormalizeProvider(provider) ?? modelConfig.GetProvider();
        return $"Модель для {entity} ({targetProvider}): {modelId}.";
    }
}
