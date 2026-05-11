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
| `shf make:endpoint <Domain>/<Operation>` | ✅ Available | Scaffold a minimal API endpoint with full OpenAPI metadata. Auto-detects GET (query) vs POST (command) from the operation name; override with `--query` / `--command`. |
| `shf make:entity <Domain>/<Name>` | 🚧 [#7](https://github.com/StrawhatsCompany/shf-cli/issues/7) | Scaffold a Domain entity. |
| `shf make:provider <Name>` | 🚧 [#8](https://github.com/StrawhatsCompany/shf-cli/issues/8) | Scaffold a provider contract + project skeleton (`Business/Providers/<Name>/` + `Providers.<Name>/`). |
| `shf make:provider-driver <Provider> <Driver>` | 🚧 [#9](https://github.com/StrawhatsCompany/shf-cli/issues/9) | Add a driver (e.g. `Smtp`, `SendGrid`) to an existing provider. |
| `shf make:persistence <postgres\|sqlserver\|sqlite>` | 🚧 [#10](https://github.com/StrawhatsCompany/shf-cli/issues/10) | Scaffold a persistence project with EF Core context + repositories + Register class. Optional `--localdb` / `--connection-string`. |
| `shf make:migration <Name>` | 🚧 [#11](https://github.com/StrawhatsCompany/shf-cli/issues/11) | Add a design-time EF Core migration to a persistence project. |

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

## `make:endpoint`

```bash
shf make:endpoint Weather/GetForecastsByCity
# writes src/WebApi/Endpoints/Weather/GetForecastsByCityEndpoint.cs:
#   - GET api/v1/Weather/GetForecastsByCity
#   - wired to Business.Features.Weather.GetForecastsByCity.GetForecastsByCityQuery
#   - Produces<Result<GetForecastsByCityResponse>>(200) + ProducesValidationProblem + 400 + 500
#   - WithName/WithSummary/WithTags filled in

shf make:endpoint Orders/PlaceOrder
# writes src/WebApi/Endpoints/Orders/PlaceOrderEndpoint.cs:
#   - POST api/v1/Orders/PlaceOrder
#   - wired to PlaceOrderCommand from the request body
#   - Produces<Result>(200) (no response payload for commands)

shf make:endpoint Weather/Settle --command --route "api/v1/weather/settle"
# explicit override: POST instead of the GET the name heuristic would pick
```

### Heuristic

Same as `make:feature`: name starts with `Get` / `List` / `Find` / `Search` / `Read` / `Browse` → GET endpoint wired to a **Query + Response** slice. Otherwise → POST wired to a **Command** slice. Override with `--query` / `--command`.

### Flags

| Flag | Default | Description |
|---|---|---|
| `--query` | — | Force GET + Query+Response wiring. |
| `--command` | — | Force POST + Command wiring (no response). |
| `--route <pattern>` | `api/v1/<Domain>/<Operation>` | Override the route. |
| `--summary <text>` | humanized operation name | OpenAPI summary. |
| `--force` | false | Overwrite existing files. |
| `--dry-run` | false | Print the file list without touching disk. |
| `--project <path>` | auto | Override the `WebApi` project location. |

The endpoint references types that live in `Business.Features.<Domain>.<Operation>`. Run `shf make:feature <Domain>/<Operation>` first if the slice doesn't exist yet — `make:endpoint` doesn't create it for you (that's intentional; the slice contract should be designed before the transport adapter).

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
