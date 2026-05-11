using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeCachingCommandTests
{
    [Fact]
    public void Generates_four_files_under_Caching_Name()
    {
        var (cmd, writer, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "Redis", DryRun = true });

        Assert.Equal(0, exit);
        AssertWroteEndingWith(writer, "Caching.Redis/Caching.Redis.csproj");
        AssertWroteEndingWith(writer, "Caching.Redis/RedisCacheProvider.cs");
        AssertWroteEndingWith(writer, "Caching.Redis/ProviderFactory.cs");
        AssertWroteEndingWith(writer, "Caching.Redis/RegisterRedisCaching.cs");
    }

    [Fact]
    public void With_package_emits_a_PackageReference_block()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeCachingCommand.Settings
        {
            Name = "Redis",
            WithPackage = "StackExchange.Redis",
            PackageVersion = "2.8.16",
            DryRun = true,
        });

        var packageReferences = ModelProperty(capture, "PackageReferences");
        Assert.Contains("<PackageReference Include=\"StackExchange.Redis\" Version=\"2.8.16\" />", packageReferences);
    }

    [Fact]
    public void With_package_defaults_version_to_wildcard()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "Redis", WithPackage = "StackExchange.Redis", DryRun = true });

        var packageReferences = ModelProperty(capture, "PackageReferences");
        Assert.Contains("Version=\"*\"", packageReferences);
    }

    [Fact]
    public void Without_package_emits_empty_PackageReferences()
    {
        var (cmd, _, _, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "Redis", DryRun = true });

        Assert.Equal(string.Empty, ModelProperty(capture, "PackageReferences"));
    }

    [Fact]
    public void Returns_nonzero_when_name_is_not_pascal_case()
    {
        var (cmd, _, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "redis", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_business_project_cannot_be_found()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindBusinessProject(Arg.Any<string>()).Returns((string?)null);
        var renderer = Substitute.For<ITemplateRenderer>();
        var writer = Substitute.For<IFileWriter>();
        var cmd = new MakeCachingCommand(locator, renderer, writer);

        var exit = cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "Redis", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Edits_CacheProviderType_when_enum_file_exists()
    {
        var (cmd, writer, _, _) = BuildCommand(enumExists: true);

        cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "Redis", DryRun = true });

        writer.Received().ApplyEdit(
            Arg.Is<string>(p => p.Replace('\\', '/').EndsWith("Business/Caching/CacheProviderType.cs")),
            Arg.Any<Func<string, string>>(),
            Arg.Any<bool>());
    }

    [Fact]
    public void Edits_SHFramework_slnx_to_add_the_new_project()
    {
        var (cmd, writer, _, _) = BuildCommand(slnxExists: true);

        cmd.Execute(Ctx(), new MakeCachingCommand.Settings { Name = "Redis", DryRun = true });

        writer.Received().ApplyEdit(
            Arg.Is<string>(p => p.EndsWith("SHFramework.slnx")),
            Arg.Any<Func<string, string>>(),
            Arg.Any<bool>());
    }

    private sealed class ModelCapture { public object? Last; }

    private static (MakeCachingCommand cmd, IFileWriter writer, ITemplateRenderer renderer, ModelCapture capture) BuildCommand(bool slnxExists = false, bool enumExists = false)
    {
        var locator = Substitute.For<IProjectLocator>();
        var businessRoot = "/repo/src/Business";
        if (slnxExists || enumExists)
        {
            EnsureFixture(slnxExists, enumExists);
            businessRoot = Path.Combine(SlnxFixtureDir, "Business");
        }
        locator.FindBusinessProject(Arg.Any<string>()).Returns(businessRoot);

        var renderer = Substitute.For<ITemplateRenderer>();
        var capture = new ModelCapture();
        renderer.Render(Arg.Any<string>(), Arg.Do<object>(m => capture.Last = m)).Returns("// generated");

        var writer = Substitute.For<IFileWriter>();
        writer.Write(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        writer.ApplyEdit(Arg.Any<string>(), Arg.Any<Func<string, string>>(), Arg.Any<bool>()).Returns("");
        return (new MakeCachingCommand(locator, renderer, writer), writer, renderer, capture);
    }

    private static readonly string SlnxFixtureDir = Path.Combine(Path.GetTempPath(), "shf-cli-tests-caching", "src");

    private static void EnsureFixture(bool slnx, bool enumFile)
    {
        Directory.CreateDirectory(Path.Combine(SlnxFixtureDir, "Business", "Caching"));
        if (slnx)
        {
            var path = Path.Combine(SlnxFixtureDir, "SHFramework.slnx");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "<Solution>\n  <Folder Name=\"/src/\">\n  </Folder>\n</Solution>\n");
            }
        }
        if (enumFile)
        {
            var path = Path.Combine(SlnxFixtureDir, "Business", "Caching", "CacheProviderType.cs");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "namespace Business.Caching;\npublic enum CacheProviderType\n{\n    InMemory = 0,\n}\n");
            }
        }
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:caching", null);

    private static void AssertWroteEndingWith(IFileWriter writer, string relativeSuffix) =>
        writer.Received().Write(
            Arg.Is<string>(p => p.Replace('\\', '/').EndsWith(relativeSuffix)),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>());

    private static string ModelProperty(ModelCapture capture, string propertyName)
    {
        Assert.NotNull(capture.Last);
        var prop = capture.Last.GetType().GetProperty(propertyName);
        Assert.NotNull(prop);
        return prop.GetValue(capture.Last)?.ToString() ?? string.Empty;
    }
}
