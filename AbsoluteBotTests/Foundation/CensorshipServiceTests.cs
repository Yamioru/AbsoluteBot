using System.Collections.Concurrent;
using AbsoluteBot.Services.UtilityServices;
using Moq;

namespace AbsoluteBotTests.Foundation;

[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
#pragma warning disable IDE0028
public class CensorshipServiceTests
{
    private readonly CensorshipService _censorshipService;
    private readonly Mock<CensorWordsService> _mockCensorWordsService;
    private readonly Mock<ConfigService> _mockConfigService;

    public CensorshipServiceTests()
    {
        _mockConfigService = new Mock<ConfigService>();
        _mockCensorWordsService = new Mock<CensorWordsService>();

        _mockCensorWordsService.Setup(x => x.GetAllCensorWords())
            .Returns(new ConcurrentBag<string>
            {
                "педр", "отсталый", "ретард", "дэбил", "дебил", "педич", "педик", "пидр", "пидор", "даун", "ниг", "маскал", "негр", "аутист", "аутяга", "дцп",
                "хитжаб", "хохл", "хохол", "Nagas", "Niga", "Nerg", "Daun", "гандон", "гондон", "долбаеб", "чурка", "хач", "faggot", "жид", "педик", "швухтель",
                "гомосек", "гамасек", "гомасек", "пидар", "pidor", "3.14door", "дэцл", "децл", "хиджаб", "симп", "smip", "пизда", "cunt", "virgin", "девствен",
                "конча", "украин", "крым", "всу", "россия", "рашка", "хахол", "хахлы", "свин", "киев", "русня", "москал", "рашкован", "солдат", "донбас",
                "наци", "путин", "луганск", "днр", "лнр", "геноцид", "херсон", "зеленск"
            });

        _mockConfigService.Setup(x => x.GetConfigValueAsync<string>("ReplacementWordForCensor"))
            .ReturnsAsync("собек");

        _censorshipService = new CensorshipService(_mockConfigService.Object, _mockCensorWordsService.Object);
    }

    [Fact]
    public async Task ApplyCensorship_ReplacesCensoredWordsAndCutsText()
    {
        await _censorshipService.InitializeAsync();

        var text = "приветn \n nop даунские дебилЮ данет?";
        var length = 30;

        var result = _censorshipService.ApplyCensorship(text, length, true);

        Assert.Equal("приветn nop собекские собекЮ...", result);
    }

    [Fact]
    public async Task ApplyCensorship_IsCaseInsensitive()
    {
        await _censorshipService.InitializeAsync();

        var result = _censorshipService.ApplyCensorship("Это ДАУН и ДеБиЛ", 200, false);

        Assert.Equal("Это собек и собек", result);
    }

    [Fact]
    public async Task ApplyCensorship_LeavesCleanTextUnchangedExceptCut()
    {
        await _censorshipService.InitializeAsync();

        var result = _censorshipService.ApplyCensorship("обычный текст без запретов", 200, false);

        Assert.Equal("обычный текст без запретов", result);
    }

    [Fact]
    public async Task ApplyCensorship_EmptyText_ReturnsEmpty()
    {
        await _censorshipService.InitializeAsync();

        var result = _censorshipService.ApplyCensorship("", 50, true);

        Assert.Equal("", result);
    }
}
