using System.Security.Cryptography;

namespace AbsoluteBot.Services;

/// <summary>
/// Генерирует детерминированную «линию отклонений» (Divergence Line).
/// Начальная точка: 29 октября 2025, значение 0.456923.
/// Все интервалы и значения псевдослучайны, но фиксированы при каждом запуске.
/// </summary>
public class DivergenceService : IAsyncInitializable
{
    private const decimal InitialDivergence = 0.456914m;
    private DateTime _anchorUtc;
    private DateTime _lastGeneratedDateUtc;
    private decimal _lastGeneratedValue;
    private Random _rng;

    public async Task InitializeAsync()
    {
        // === Детерминированный сид (скрытый) ===
        unchecked
        {
            var partA = "U3RlaW5zO0dhdGU6RGl2ZXJnaW5jZQ=="; // "Steins;Gate:Divergence"
            var partB = "bWV0ZXI="; // "meter"

            var a = Convert.FromBase64String(partA);
            var b = Convert.FromBase64String(partB);

            for (var i = 0; i < a.Length; i++) a[i] ^= (byte) (31 + i * 17);
            for (var i = 0; i < b.Length; i++) b[i] ^= (byte) (97 - i * 13);

            using var sha = SHA256.Create();
            var seedBytes = sha.ComputeHash(a.Concat(b).ToArray());

            var seed = BitConverter.ToInt32(seedBytes, 0);
            if (seed < 0) seed = ~seed;

            _rng = new Random(seed);
        }

        // === Якорная дата (от которой считаются интервалы) ===
        _anchorUtc = new DateTime(2026, 02, 03, 0, 0, 0, DateTimeKind.Utc);

        _lastGeneratedDateUtc = _anchorUtc;
        _lastGeneratedValue = InitialDivergence;
    }

    /// <summary>
    /// Возвращает текущее детерминированное значение отклонения (например "0.456923").
    /// При каждом запуске программы для одной и той же даты будет одно и то же значение.
    /// </summary>
    public string GetCurrentDivergence()
    {
        var nowUtc = DateTime.UtcNow;

        if (nowUtc <= _anchorUtc)
            return InitialDivergence.ToString("F6");

        while (_lastGeneratedDateUtc < nowUtc)
        {
            var next = NextChange(_lastGeneratedDateUtc);
            if (next.dateUtc <= nowUtc)
            {
                _lastGeneratedDateUtc = next.dateUtc;
                _lastGeneratedValue = next.value;
            }
            else
            {
                break;
            }
        }

        return _lastGeneratedValue.ToString("F6");
    }

    // === Приватная генерация следующего изменения ===
    private (DateTime dateUtc, decimal value) NextChange(DateTime from)
    {
        var days = _rng.Next(1, 366); // интервал 1–365 дней
        var nextDate = from.AddDays(days);

        var raw = _rng.NextDouble() * 2.0; // значение 0–2
        var val = Math.Round((decimal) raw, 6, MidpointRounding.AwayFromZero);
        if (val >= 2.000000m) val = 1.999999m;

        return (nextDate, val);
    }
}