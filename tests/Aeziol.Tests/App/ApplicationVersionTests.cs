using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class ApplicationVersionTests
{
    [Theory]
    [InlineData("0.9.0-beta.1", "0.9.0-beta.1")]
    [InlineData("0.9.0-beta.1+4f25aa1", "0.9.0-beta.1")]
    [InlineData("1.0.0+build.42", "1.0.0")]
    public void Normalize_PreservesThePublicSemanticVersion(string informational, string expected)
    {
        Assert.Equal(expected, ApplicationVersion.Normalize(informational, new Version(9, 9, 9)));
    }

    [Fact]
    public void Normalize_FallsBackToTheAssemblyVersion()
    {
        Assert.Equal("0.9.0", ApplicationVersion.Normalize(null, new Version(0, 9, 0, 12)));
    }
}
