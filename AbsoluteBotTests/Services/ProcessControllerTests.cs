using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ProcessControllerTests
{
    [Fact]
    public void IsRunningInContainer_True_WhenDotnetContainerEnvSet()
    {
        var previous = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");
            Assert.True(ProcessController.IsRunningInContainer());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", previous);
        }
    }

    [Fact]
    public void IsRunningInContainer_False_WhenEnvUnset_AndNoDockerenv()
    {
        var previous = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null);
            if (File.Exists("/.dockerenv"))
                return;
            Assert.False(ProcessController.IsRunningInContainer());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", previous);
        }
    }
}
