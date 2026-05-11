using Shf.Cli.Commands.Make;
using Shf.Cli.Services;
using Spectre.Console.Cli;

namespace Shf.Cli.Tests.Commands.Make;

public class MakeEntityCommandTests
{
    [Fact]
    public void Defaults_include_Id_and_CreatedAt_as_class_properties()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "Weather/Forecast", DryRun = true });

        var props = CapturedPropertiesFrom(capture);
        Assert.Contains("public Guid Id { get; set; }", props);
        Assert.Contains("public DateTimeOffset CreatedAt { get; set; }", props);
    }

    [Fact]
    public void No_id_skips_the_Id_property()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "Weather/Forecast", NoId = true, DryRun = true });

        var props = CapturedPropertiesFrom(capture);
        Assert.DoesNotContain("Guid Id", props);
        Assert.Contains("DateTimeOffset CreatedAt", props);
    }

    [Fact]
    public void No_timestamp_skips_the_CreatedAt_property()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "Weather/Forecast", NoTimestamp = true, DryRun = true });

        var props = CapturedPropertiesFrom(capture);
        Assert.Contains("Guid Id", props);
        Assert.DoesNotContain("DateTimeOffset CreatedAt", props);
    }

    [Fact]
    public void Custom_properties_are_appended_after_defaults()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings
        {
            Name = "Weather/Forecast",
            Properties = "Date:DateOnly,TemperatureC:int,Summary:string?",
            DryRun = true,
        });

        var props = CapturedPropertiesFrom(capture);
        Assert.Contains("public DateOnly Date { get; set; }", props);
        Assert.Contains("public int TemperatureC { get; set; }", props);
        Assert.Contains("public string? Summary { get; set; }", props);
    }

    [Fact]
    public void Record_emits_positional_parameter_list_on_one_line()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings
        {
            Name = "Weather/Forecast",
            AsRecord = true,
            NoId = true,
            NoTimestamp = true,
            Properties = "Date:DateOnly,TemperatureC:int",
            DryRun = true,
        });

        var props = CapturedPropertiesFrom(capture);
        Assert.Equal("DateOnly Date, int TemperatureC", props);
    }

    [Fact]
    public void Record_uses_Record_template()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings
        {
            Name = "Weather/Forecast",
            AsRecord = true,
            DryRun = true,
        });

        renderer.Received().Render(Arg.Is<string>(p => p.EndsWith("Record.cs.sbn")), Arg.Any<object>());
    }

    [Fact]
    public void Class_uses_Class_template()
    {
        var (cmd, _, renderer, capture) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "Weather/Forecast", DryRun = true });

        renderer.Received().Render(Arg.Is<string>(p => p.EndsWith("Class.cs.sbn")), Arg.Any<object>());
    }

    [Fact]
    public void Writes_to_Entities_subfolder_named_after_the_domain()
    {
        var (cmd, writer, _, _) = BuildCommand();

        cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "Weather/Forecast", DryRun = true });

        writer.Received().Write(
            Arg.Is<string>(p => p.Replace('\\', '/').EndsWith("Domain/Entities/Weather/Forecast.cs")),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>());
    }

    [Fact]
    public void Returns_nonzero_when_name_is_malformed()
    {
        var (cmd, _, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "JustName", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_no_properties_remain_after_skips()
    {
        var (cmd, _, _, _) = BuildCommand();

        var exit = cmd.Execute(Ctx(), new MakeEntityCommand.Settings
        {
            Name = "Weather/Forecast",
            NoId = true,
            NoTimestamp = true,
            DryRun = true,
        });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Returns_nonzero_when_domain_project_cannot_be_found()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindDomainProject(Arg.Any<string>()).Returns((string?)null);
        var renderer = Substitute.For<ITemplateRenderer>();
        var writer = Substitute.For<IFileWriter>();
        var cmd = new MakeEntityCommand(locator, renderer, writer);

        var exit = cmd.Execute(Ctx(), new MakeEntityCommand.Settings { Name = "Weather/Forecast", DryRun = true });

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Property_parser_rejects_entries_without_colon()
    {
        Assert.Throws<ArgumentException>(() => MakeEntityCommand.ParseProperties("Name:string,JustName").ToList());
    }

    private sealed class ModelCapture
    {
        public object? Last;
    }

    private static (MakeEntityCommand cmd, IFileWriter writer, ITemplateRenderer renderer, ModelCapture capture) BuildCommand()
    {
        var locator = Substitute.For<IProjectLocator>();
        locator.FindDomainProject(Arg.Any<string>()).Returns("/repo/src/Domain");
        var renderer = Substitute.For<ITemplateRenderer>();
        var capture = new ModelCapture();
        renderer.Render(Arg.Any<string>(), Arg.Do<object>(m => capture.Last = m)).Returns("// generated");
        var writer = Substitute.For<IFileWriter>();
        writer.Write(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        return (new MakeEntityCommand(locator, renderer, writer), writer, renderer, capture);
    }

    private static CommandContext Ctx() => new([], Substitute.For<IRemainingArguments>(), "make:entity", null);

    private static string CapturedPropertiesFrom(ModelCapture capture)
    {
        Assert.NotNull(capture.Last);
        var prop = capture.Last.GetType().GetProperty("Properties");
        Assert.NotNull(prop);
        return prop.GetValue(capture.Last)?.ToString() ?? string.Empty;
    }

    private static string CapturedProperties(ITemplateRenderer renderer)
    {
        object? captured = null;
        renderer.Received().Render(Arg.Any<string>(), Arg.Do<object>(m => captured = m));
        Assert.NotNull(captured);
        var prop = captured.GetType().GetProperty("Properties");
        Assert.NotNull(prop);
        return prop.GetValue(captured)?.ToString() ?? string.Empty;
    }
}
