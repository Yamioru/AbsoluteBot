using System.Diagnostics;
using System.Reflection;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Прод-реализация перезапуска и выключения приложения.
///     В Docker достаточно Exit: у контейнера <c>restart: always</c>.
///     Локально поднимается новый процесс dotnet, затем текущий завершается.
/// </summary>
public class ProcessController : IProcessController
{
    private const int RestartReplyDelayMs = 1500;

    public void Restart()
    {
        Thread.Sleep(RestartReplyDelayMs);

        if (IsRunningInContainer())
        {
            Environment.Exit(0);
            return;
        }

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

    /// <summary>
    ///     Docker/Kubernetes: не нужно запускать второй процесс внутри того же контейнера.
    /// </summary>
    internal static bool IsRunningInContainer() =>
        File.Exists("/.dockerenv") ||
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true",
            StringComparison.OrdinalIgnoreCase);
}
