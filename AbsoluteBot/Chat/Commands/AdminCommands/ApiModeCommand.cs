using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
/// Переключает бесплатный и платный режим API.
/// В бесплатном инструмент Google Search не вызывается и <c>!загугли</c> скрыта.
/// </summary>
public class ApiModeCommand(NeuralModelConfigService modelConfig) : BaseCommand, IParameterized
{
    public override int Priority => 707;
    public override string Description => "переключает бесплатный и платный режим API.";
    public override string Name => "!apimode";
    public string Parameters => "free или paid";

    public override bool CanExecute(ParsedCommand command)
    {
        return CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var tier = NeuralModelConfigService.NormalizeTier(command.Parameters.Trim());
        if (tier == null)
            return $"Использование: {Name} free|paid";

        if (!await modelConfig.SetTierAsync(tier).ConfigureAwait(false))
            return "Не удалось переключить режим API.";

        return tier == NeuralTiers.Paid
            ? "Режим API: платный. Команда !загугли снова ищет в интернете."
            : "Режим API: бесплатный. Поиск Google у моделей выключен.";
    }
}