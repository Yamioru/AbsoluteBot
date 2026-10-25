using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Переключает текстовую нейросеть между Gemini и Groq.
/// </summary>
public class SwitchAiCommand(NeuralModelConfigService modelConfig) : BaseCommand, IParameterized
{
    public override int Priority => 706;
    public override string Description => "переключает нейросеть между gemini и groq.";
    public override string Name => "!switchai";
    public string Parameters => "gemini или groq";

    public override bool CanExecute(ParsedCommand command) =>
        CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var provider = NeuralModelConfigService.NormalizeProvider(command.Parameters.Trim());
        if (provider == null)
            return $"Использование: {Name} gemini|groq";

        if (!await modelConfig.SetProviderAsync(provider).ConfigureAwait(false))
            return "Не удалось переключить провайдера нейросети.";

        return $"Нейросеть переключена на {provider}.";
    }
}
