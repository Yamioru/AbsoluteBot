namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Абстракция управления жизненным циклом процесса приложения (для тестируемости Restart/Shutdown).
/// </summary>
public interface IProcessController
{
    void Restart();
    void Shutdown();
}
