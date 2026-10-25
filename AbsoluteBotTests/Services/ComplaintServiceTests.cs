using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ComplaintServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task AddComplaint_AppearsInLastComplaints()
    {
        _ = fixture;
        var service = new ComplaintService();
        await service.InitializeAsync();

        var ok = await service.AddComplaintAsync("test-complaint-xyz");
        Assert.True(ok);

        var last = service.GetLastComplaints(5);
        Assert.Contains("test-complaint-xyz", last);
    }
}
