using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
/// После ответа Gemini «high demand» модель не вызывается три часа: запросы сразу идут в замену.
/// </summary>
public static class GeminiDemandCooldown
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(3);
    private static readonly object Gate = new();
    private static readonly Dictionary<string, DateTime> Until = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsCooling(string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return false;
        lock (Gate)
        {
            if (!Until.TryGetValue(model, out var until)) return false;
            if (until > DateTime.UtcNow) return true;
            Until.Remove(model);
            return false;
        }
    }

    public static void Note(string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return;
        var until = DateTime.UtcNow.Add(Duration);
        lock (Gate)
        {
            Until[model] = until;
        }

        Log.Warning("Модель {Model} перегружена. До {Until:HH:mm} UTC запросы идут в замену, ключи этой модели не перебираю.",
            model, until);
    }

    /// <summary>
    /// Убирает модели, которые ещё в паузе. Если пауза у всех, остаётся замена, а без замены — исходный список.
    /// </summary>
    public static IReadOnlyList<string> PreferAvailable(IReadOnlyList<string> models)
    {
        if (models.Count == 0) return models;
        List<string>? available = null;
        foreach (var model in models)
        {
            if (IsCooling(model))
            {
                available ??= new List<string>(models.Count);
                continue;
            }

            if (available != null)
                available.Add(model);
        }

        if (available == null) return models;
        if (available.Count > 0) return available;
        return models.Count > 1 ? models.Skip(1).ToList() : models;
    }

    public static void Reset()
    {
        lock (Gate)
        {
            Until.Clear();
        }
    }
}