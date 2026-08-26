namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
///     Поиск и запись значений в словарях, ключ которых — никнейм.
/// </summary>
internal static class NickKeyedStore
{
    public static bool TryGet<T>(IDictionary<string, T> data, IReadOnlyList<string> aliases, out T value)
    {
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            if (data.TryGetValue(alias.ToLower(), out value!))
                return true;
        }

        value = default!;
        return false;
    }

    public static bool TryGetExact<T>(IDictionary<string, T> data, IReadOnlyList<string> aliases, out T value)
    {
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            if (data.TryGetValue(alias, out value!))
                return true;
            if (data.TryGetValue(alias.ToLower(), out value!))
                return true;
        }

        value = default!;
        return false;
    }

    public static void Set<T>(IDictionary<string, T> data, IReadOnlyList<string> aliases, string current, T value)
    {
        data[current.ToLower()] = value;
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            var key = alias.ToLower();
            if (data.ContainsKey(key))
                data[key] = value;
        }
    }

    public static void SetExact<T>(IDictionary<string, T> data, IReadOnlyList<string> aliases, string current, T value)
    {
        data[current] = value;
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            if (data.ContainsKey(alias))
                data[alias] = value;
            var lower = alias.ToLower();
            if (!string.Equals(alias, lower, StringComparison.Ordinal) && data.ContainsKey(lower))
                data[lower] = value;
        }
    }
}
