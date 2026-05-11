using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class SlnxMutatorTests
{
    [Fact]
    public void Inserts_project_in_alphabetical_order()
    {
        const string before = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="Business.Services/Business.Services.csproj" />
                <Project Path="Business/Business.csproj" />
                <Project Path="Providers.Mail/Providers.Mail.csproj" />
                <Project Path="WebApi/WebApi.csproj" />
              </Folder>
            </Solution>
            """;

        var result = SlnxMutator.AddProjectToSrcFolder(before, "Providers.Sms/Providers.Sms.csproj");

        // Sms comes after Mail and before WebApi.
        var idxMail = result.IndexOf("Providers.Mail/", StringComparison.Ordinal);
        var idxSms = result.IndexOf("Providers.Sms/", StringComparison.Ordinal);
        var idxWebApi = result.IndexOf("WebApi/", StringComparison.Ordinal);
        Assert.True(idxMail < idxSms);
        Assert.True(idxSms < idxWebApi);
    }

    [Fact]
    public void Idempotent_when_project_already_listed()
    {
        const string before = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="Providers.Sms/Providers.Sms.csproj" />
              </Folder>
            </Solution>
            """;

        var result = SlnxMutator.AddProjectToSrcFolder(before, "Providers.Sms/Providers.Sms.csproj");

        Assert.Equal(before, result);
    }

    [Fact]
    public void Inserts_into_empty_src_folder()
    {
        const string before = """
            <Solution>
              <Folder Name="/src/">
              </Folder>
            </Solution>
            """;

        var result = SlnxMutator.AddProjectToSrcFolder(before, "Providers.Sms/Providers.Sms.csproj");

        Assert.Contains("Providers.Sms/Providers.Sms.csproj", result);
    }

    [Fact]
    public void Throws_when_src_folder_is_missing()
    {
        const string before = "<Solution></Solution>";

        Assert.Throws<InvalidOperationException>(() =>
            SlnxMutator.AddProjectToSrcFolder(before, "Providers.Sms/Providers.Sms.csproj"));
    }
}
