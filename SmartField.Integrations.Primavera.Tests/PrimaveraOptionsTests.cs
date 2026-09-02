using SmartField.Integrations.Primavera;

namespace SmartField.Integrations.Primavera.Tests;

public class PrimaveraOptionsTests
{
    [Fact]
    public void IsConfigured_ReturnsTrueWithBaseUrlCompanyAndApiKey()
    {
        var options = new PrimaveraOptions
        {
            BaseUrl = "https://primavera.sysprime.local",
            Company = "DEMO",
            ApiKey = "configured-outside-code"
        };

        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsTrueWithBaseUrlCompanyUsernameAndPassword()
    {
        var options = new PrimaveraOptions
        {
            BaseUrl = "https://primavera.sysprime.local",
            Company = "DEMO",
            Username = "integration-user",
            Password = "configured-outside-code"
        };

        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsFalseWhenCredentialsAreMissing()
    {
        var options = new PrimaveraOptions
        {
            BaseUrl = "https://primavera.sysprime.local",
            Company = "DEMO"
        };

        Assert.False(options.IsConfigured);
    }
}
