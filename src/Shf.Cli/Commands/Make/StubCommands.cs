using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shf.Cli.Commands.Make;

public abstract class StubCommand(string name) : Command<StubCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[args]")]
        [Description("Ignored — this command is not yet implemented.")]
        public string[] Args { get; init; } = [];
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine($"[yellow]{name}[/] is not yet implemented — see the shf-cli repo backlog.");
        return 64;
    }
}

public sealed class MakeMigrationCommand() : StubCommand("make:migration");
