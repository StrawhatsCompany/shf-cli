# CLAUDE.md

Guidance for Claude Code when working in this repo.

## What this is

The `shf` CLI — a `dotnet tool` that scaffolds parts of a [Strawhats Framework](https://github.com/StrawhatsCompany/sh-framework-template) service. Laravel-Artisan-style command surface: `shf make:feature`, `shf make:endpoint`, etc. Installed via `dotnet tool install -g StrawhatsCompany.SHFramework.Cli`.

## Layout

| Path | Role |
|---|---|
| `src/Shf.Cli/` | The CLI executable. Packs as a dotnet tool (`PackAsTool=true`, `ToolCommandName=shf`). |
| `src/Shf.Cli/Commands/Make/` | One file per generator command. |
| `src/Shf.Cli/Services/` | Cross-command primitives: project locator, template renderer, file writer. |
| `src/Shf.Cli/Templates/<Generator>/` | `*.sbn` templates. Token syntax: `{{ Identifier }}`. Copied to output via `<None Update>` in the csproj. |
| `src/Shf.Cli/Infrastructure/` | Spectre.Console.Cli ↔ Microsoft DI adapter. |
| `tests/Shf.Cli.Tests/` | xUnit + NSubstitute. One test class per command. |

Target framework: `net10.0`.

## Adding a new generator

1. Create `src/Shf.Cli/Commands/Make/Make<Name>Command.cs` extending `Command<Settings>`.
2. Add templates under `src/Shf.Cli/Templates/<Generator>/<File>.cs.sbn`.
3. Register the command in `Program.cs` with `config.AddCommand<...>("make:<name>")`.
4. Replace the stub class in `StubCommands.cs` if one exists.
5. Add tests under `tests/Shf.Cli.Tests/Commands/Make/Make<Name>CommandTests.cs`. Mock `IProjectLocator`, `ITemplateRenderer`, `IFileWriter`. Use `--dry-run` and assert on `IFileWriter.Write` calls.
6. Update the table in `README.md` flipping the status to ✅.

## Conventions

- **Spectre.Console.Cli** for command parsing. `Command<Settings>` with one nested `Settings : CommandSettings`.
- **No 3rd-party templating engine** — `TokenTemplateRenderer` does `{{ Identifier }}` substitution against model properties via reflection. Keep it small; don't pull in Scriban/Liquid/etc. unless we genuinely need control flow.
- **Templates copy to output** via `<None Update="Templates\**\*.sbn"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`. Reference them at runtime via `Path.Combine(AppContext.BaseDirectory, "Templates", ...)`.
- **`--dry-run` on every generator** — print the planned file list without touching disk. `IFileWriter.Write(path, content, overwrite, dryRun)` handles it centrally.
- **`--force` overrides existing files.** Default behavior is to refuse to overwrite.
- **Project locator first, then operate.** Every generator should auto-detect the target project. Manual override via `--project`.
- **Exit codes:** 0 = success, 1 = user error (bad input, file conflict), 64 = command not implemented.

## Conventions inherited from sh-framework-template

The generated code must follow the rules in [sh-framework-template/CLAUDE.md](https://github.com/StrawhatsCompany/sh-framework-template/blob/main/CLAUDE.md). When in doubt about what shape a generated file should take, read that file — this repo is downstream of it.

Specifically:
- Vertical slicing layout (`src/Business/Features/<Domain>/<Operation>/`).
- One public type per file.
- `sealed` by default.
- Primary constructors for DI.
- Result/Result&lt;T&gt; over exceptions.
- xUnit + NSubstitute (no FluentAssertions).
