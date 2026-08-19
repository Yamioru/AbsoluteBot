using System.Diagnostics;
using System.Reflection;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Прод-реализация перезапуска и выключения приложения.
/// </summary>
public class ProcessController : IProcessController
{
    public void Restart()
    {
        var dllPath = Assembly.GetExecutingAssembly().Location;
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = dllPath,
            UseShellExecute = false
        };

        Process.Start(processStartInfo);
        Environment.Exit(0);
    }

    public void Shutdown()
    {
        Environment.Exit(0);
    }
}
