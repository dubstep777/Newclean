using System.Reflection;
using XUnlock.Services;

namespace XUnlock.Tests;

public class ProjectIntegrityTests
{
    [Fact]
    public void ParserIsPublicAndLoadable()
    {
        Assert.NotNull(typeof(ApiResponseParser));
        Assert.NotNull(typeof(TimingEngine));
    }

    [Fact]
    public void MainAssemblyLoads()
    {
        var name = typeof(CommunityApiClient).Assembly.GetName();
        Assert.Equal("XUnlock", name.Name);
    }
}
