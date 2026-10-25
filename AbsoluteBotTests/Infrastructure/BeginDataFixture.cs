using AbsoluteBot.Services.UtilityServices;
using Xunit;

namespace AbsoluteBotTests.Infrastructure;

/// <summary>
///     Копирует BeginData во временную папку и выставляет ABSOLUTEBOT_DATA_ROOT.
///     Живые файлы BeginData не изменяются.
/// </summary>
public sealed class BeginDataFixture : IDisposable
{
    private readonly string? _previousRoot;

    public string TempRoot { get; }
    public string SourceBeginData { get; }

    public BeginDataFixture()
    {
        SourceBeginData = FindBeginDataDirectory()
            ?? throw new DirectoryNotFoundException("Не удалось найти папку BeginData относительно тестовой сборки.");

        TempRoot = Path.Combine(Path.GetTempPath(), "AbsoluteBotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRoot);

        foreach (var file in Directory.GetFiles(SourceBeginData))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("logs", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(TempRoot, name), overwrite: true);
        }

        // Словари hunspell и прочие подпапки (кроме logs)
        foreach (var dir in Directory.GetDirectories(SourceBeginData))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.Equals("logs", StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(dir, Path.Combine(TempRoot, dirName));
        }

        _previousRoot = Environment.GetEnvironmentVariable(DataPaths.DataRootEnvironmentVariable);
        Environment.SetEnvironmentVariable(DataPaths.DataRootEnvironmentVariable, TempRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DataPaths.DataRootEnvironmentVariable, _previousRoot);
        try
        {
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static string? FindBeginDataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "BeginData");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "config.json")))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var sub in Directory.GetDirectories(source))
            CopyDirectory(sub, Path.Combine(destination, Path.GetFileName(sub)));
    }
}

[CollectionDefinition("BeginData")]
public class BeginDataCollection : ICollectionFixture<BeginDataFixture>
{
}
