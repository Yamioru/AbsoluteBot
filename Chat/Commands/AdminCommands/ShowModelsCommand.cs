using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Показывает текущего провайдера и модели по сущностям.
/// </summary>
public class ShowModelsCommand(NeuralModelConfigService modelConfig) : BaseCommand
{
    public override int Priority => 707;
    public override string Description => "показывает текущего провайдера и модели нейросети.";
    public override string Name => "!showmodels";

    public override bool CanExecute(ParsedCommand command) =>
        CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command) =>
        Task.FromResult(modelConfig.FormatForDisplay());
}
