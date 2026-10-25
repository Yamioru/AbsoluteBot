using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
/// Задаёт модель для сущности (Chat, Ask, GoogleSearch, Image).
/// </summary>
public class SetModelCommand(NeuralModelConfigService modelConfig) : BaseCommand, IParameterized
{
    public override int Priority => 708;
    public override string Description => "задаёт основную модель и необязательную замену.";
    public override string Name => "!setmodel";
    public string Parameters => "сущность [gemini|groq] основная [замена|-]";

    public override bool CanExecute(ParsedCommand command)
    {
        return CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parts = command.Parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return $"Использование: {Name} Chat|Ask|GoogleSearch|Image [gemini|groq] основная [замена|-]";

        var entity = NeuralModelConfigService.NormalizeEntity(parts[0]);
        if (entity == null)
            return "Неизвестная сущность. Доступны: Chat, Ask, GoogleSearch, Image.";

        string? provider = null;
        string modelId;
        string? fallback = null;
        var setFallback = false;
        var providerAt = parts.Length >= 3 ? NeuralModelConfigService.NormalizeProvider(parts[1]) : null;
        if (providerAt != null)
        {
            provider = parts[1];
            modelId = parts[2];
            if (parts.Length >= 4)
            {
                fallback = string.Join(' ', parts.Skip(3));
                setFallback = true;
            }
        }
        else
        {
            modelId = parts[1];
            if (parts.Length >= 3)
            {
                fallback = string.Join(' ', parts.Skip(2));
                setFallback = true;
            }
        }

        if (!await modelConfig.SetModelAsync(entity, modelId, provider, fallback, setFallback).ConfigureAwait(false))
            return "Не удалось сохранить модель.";

        var targetProvider = entity == NeuralEntities.Image
            ? NeuralProviders.Gemini
            : NeuralModelConfigService.NormalizeProvider(provider) ?? modelConfig.GetProvider();
        var attempts = modelConfig.GetAttemptModels(entity, targetProvider);
        var replacement = attempts.Count > 1 ? attempts[1] : "не задана";
        return $"Модель для {entity} ({targetProvider}): {attempts[0]}, замена {replacement}.";
    }
}