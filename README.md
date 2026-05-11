# shf-cli

A Laravel-Artisan-style code generator for [Strawhats Framework](https://github.com/StrawhatsCompany/sh-framework-template) services. Scaffolds the parts (feature slices, endpoints, entities, providers, persistence projects) the framework expects, so you can keep typing in one place and `shf make:thing`.

## Install

```bash
dotnet tool install -g StrawhatsCompany.SHFramework.Cli
```

This installs the `shf` command on your PATH.

## Commands

| Command | Status | What it does |
|---|---|---|
| `shf make:feature <Domain>/<Operation>` | ✅ Available | Scaffold a CQRS slice (request, handler, optional response). Auto-detects Query vs Command from the operation name. |
| `shf make:endpoint <name>` | 🚧 Backlog | Scaffold a minimal API endpoint with full OpenAPI metadata. |
| `shf make:entity <Domain>/<Name>` | 🚧 Backlog | Scaffold a Domain entity. |
| `shf make:provider <Name>` | 🚧 Backlog | Scaffold a provider contract + project skeleton (`Business/Providers/<Name>/` + `Providers.<Name>/`). |
| `shf make:provider-driver <Provider> <Driver>` | 🚧 Backlog | Add a driver (e.g. `Smtp`, `SendGrid`) to an existing provider. |
| `shf make:persistence <postgres\|sqlserver\|sqlite>` | 🚧 Backlog | Scaffold a persistence project with EF Core context + repositories + Register class. Optional `--localdb` / `--connection-string`. |
| `shf make:migration <Name>` | 🚧 Backlog | Add a design-time EF Core migration to a persistence project. |

## `make:feature`

```bash
shf make:feature Weather/GetForecastsByCity
# writes:
#   src/Business/Features/Weather/GetForecastsByCity/GetForecastsByCityQuery.cs
#   src/Business/Features/Weather/GetForecastsByCity/GetForecastsByCityHandler.cs
#   src/Business/Features/Weather/GetForecastsByCity/GetForecastsByCityResponse.cs

shf make:feature Mails/SendMail
# writes:
#   src/Business/Features/Mails/SendMail/SendMailCommand.cs
#   src/Business/Features/Mails/SendMail/SendMailHandler.cs
```

### Heuristic

If the operation name starts with `Get` / `List` / `Find` / `Search` / `Read` / `Browse` the slice is generated as a **Query + Response**. Otherwise it's a **Command** with no response. Override with `--query` / `--command`. Skip the response with `--no-response`.

### Flags

| Flag | Default | Description |
|---|---|---|
| `--query` | — | Force Query+Response shape. |
| `--command` | — | Force Command shape (no response). |
| `--no-response` | false | When generating a query, skip the Response class. |
| `--force` | false | Overwrite existing files. |
| `--dry-run` | false | Print the file list without touching disk. |
| `--project <path>` | auto | Override the `Business` project location. By default `shf` walks up from cwd looking for `src/Business/Business.csproj`. |

## Build from source

```bash
git clone git@github.com:StrawhatsCompany/shf-cli.git
cd shf-cli
dotnet build
dotnet test
dotnet pack src/Shf.Cli -c Release -o ./artifacts
dotnet tool install -g --add-source ./artifacts StrawhatsCompany.SHFramework.Cli
```

## Contributing

Each generator is its own issue on the backlog. Pick one, implement it, open a PR. Templates live under `src/Shf.Cli/Templates/<Generator>/`. The token format is `{{ Identifier }}`, mapped to public properties on the model passed to `ITemplateRenderer.Render`.
