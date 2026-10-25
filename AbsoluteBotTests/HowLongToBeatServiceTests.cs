using AbsoluteBot.Services;

namespace AbsoluteBotTests;

[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("Area", "Services")]
public class HowLongToBeatServiceTests
{
    private readonly HowLongToBeatService _howLongToBeatService = new(new HttpClient());

    [Theory]
    [InlineData("Shin Megami Tensei: Persona 3 Portable", 7389)]
    [InlineData("World of warcraft", 334456)]
    [InlineData("Just Chatting", 0)]
    public async Task GetGameDurationAsync_ReturnsExpectedDuration(string gameName, int expectedDuration)
    {
        var result = await _howLongToBeatService.GetGameDurationAsync(gameName);

        if (expectedDuration == 0)
            Assert.Equal(0, result);
        else
            Assert.True(IsWithinTolerance(result, expectedDuration, 0.05),
                $"Результат {result} не находится в пределах допустимой погрешности для игры {gameName}.");
    }

    private static bool IsWithinTolerance(int actual, int expected, double tolerance)
    {
        var lowerBound = expected * (1 - tolerance);
        var upperBound = expected * (1 + tolerance);
        return actual >= lowerBound && actual <= upperBound;
    }
}
