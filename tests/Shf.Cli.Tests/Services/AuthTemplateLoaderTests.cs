using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class AuthTemplateLoaderTests
{
    [Fact]
    public void LoadAll_finds_all_ten_authentication_templates_packaged_with_the_cli()
    {
        var loader = new AuthTemplateLoader();
        var templates = loader.LoadAll();

        // 10 = foundations + tenant + identity + jwt + refresh + apikey + mfa-totp + mfa-email + mfa-sms + sso
        Assert.Equal(10, templates.Count);
    }

    [Fact]
    public void Every_template_has_slug_and_title()
    {
        var loader = new AuthTemplateLoader();
        var templates = loader.LoadAll();

        Assert.All(templates, t =>
        {
            Assert.False(string.IsNullOrEmpty(t.Slug));
            Assert.False(string.IsNullOrEmpty(t.Title));
        });
    }

    [Fact]
    public void Foundations_template_loads_with_expected_metadata()
    {
        var loader = new AuthTemplateLoader();
        var foundations = loader.LoadAll().Single(t => t.Slug == "foundations");

        Assert.Contains("entity foundations", foundations.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(foundations.DependsOn);
        Assert.Contains("enhancement", foundations.Labels);
        Assert.Contains("version:minor", foundations.Labels);
    }

    [Fact]
    public void Dependency_chain_jwt_to_refresh_to_sso()
    {
        var loader = new AuthTemplateLoader();
        var bySlug = loader.LoadAll().ToDictionary(t => t.Slug);

        Assert.Contains("identity", bySlug["jwt"].DependsOn);
        Assert.Contains("jwt", bySlug["refresh"].DependsOn);
        Assert.Contains("refresh", bySlug["sso"].DependsOn);
    }

    [Fact]
    public void Templates_carry_body_content()
    {
        var loader = new AuthTemplateLoader();
        Assert.All(loader.LoadAll(), t =>
        {
            Assert.NotNull(t.Body);
            Assert.NotEmpty(t.Body);
        });
    }
}
