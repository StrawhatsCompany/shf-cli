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
| `shf make:entity <Domain>/<Name>` | ✅ Available | Scaffold a Domain entity (class or record) with `Id` + `CreatedAt` defaults plus user-specified properties. |
| `shf make:provider <Name>` | ✅ Available | Scaffold a provider contract + project skeleton (`Business/Providers/<Name>/` + `Providers.<Name>/`). Optionally seeds a first driver. Updates `SHFramework.slnx`. |
| `shf make:provider-driver <Provider> <Driver>` | ✅ Available | Add a driver to an existing provider — generates the driver class, adds it to the `ProviderType` enum, and rewrites the factory switch in place. |
| `shf make:persistence <postgres\|sqlserver\|sqlite\|couchbase>` | ✅ Available | Scaffold a persistence project. EF Core variants (`postgres` / `sqlserver` / `sqlite`) emit DbContext + design-time factory; `couchbase` emits Couchbase SDK wiring (`AddCouchbase` + bucket provider, no EF). Edits `SHFramework.slnx` and both `appsettings` files. `--localdb` / `--connection-string` available. |
| `shf make:caching <Name>` | ✅ Available | Scaffold a caching provider project (`Caching.<Name>/`) implementing `ICacheProvider`. Mutates `CacheProviderType` and `SHFramework.slnx`. `--with-package` adds a NuGet reference (e.g., `StackExchange.Redis`). |
| `shf make:migration <Name>` | ✅ Available | Wrap `dotnet ef migrations add` with auto-detected persistence + startup projects. |

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

## `make:entity`

```bash
shf make:entity Orders/Order --properties "CustomerName:string?,Amount:decimal,Status:string?"
# writes src/Domain/Entities/Orders/Order.cs:
#   - public sealed class Order with Id (Guid), CreatedAt (DateTimeOffset),
#     CustomerName, Amount, Status

shf make:entity Weather/Forecast --record --no-id --no-timestamp \
    --properties "Date:DateOnly,TemperatureC:int,Summary:string?"
# writes a positional record with just the user-specified properties
```

### Defaults

Every entity gets `Id` (`Guid`) and `CreatedAt` (`DateTimeOffset`) unless you opt out with `--no-id` / `--no-timestamp`. User-specified `--properties` are appended after.

### Property list

Comma-separated `Name:Type` pairs. Types are emitted verbatim, so `string?` makes the property nullable, `List<string>` and `decimal` work, etc.

### Flags

| Flag | Default | Description |
|---|---|---|
| `--properties <list>` | — | Comma-separated `Name:Type` pairs. |
| `--record` | false | Emit a positional record instead of a class with `{ get; set; }` properties. |
| `--no-id` | false | Skip the default `Id` property. |
| `--no-timestamp` | false | Skip the default `CreatedAt` property. |
| `--force` | false | Overwrite existing files. |
| `--dry-run` | false | Print the file list without touching disk. |
| `--project <path>` | auto | Override the `Domain` project location. |

## `make:provider`

```bash
shf make:provider Sms
# writes:
#   src/Business/Providers/Sms/ISmsProvider.cs           (marker interface)
#   src/Business/Providers/Sms/SmsProviderCredential.cs  (extends ProviderCredential<SmsProviderType>)
#   src/Business/Providers/Sms/SmsProviderType.cs        (empty enum)
#   src/Providers.Sms/Providers.Sms.csproj
#   src/Providers.Sms/ProviderFactory.cs                 (IProviderFactory<...>, NotSupportedException only)
#   src/Providers.Sms/RegisterSmsProvider.cs             (AddSmsProvider() DI extension)
#   src/Providers.Sms/SmsProviderResultCode.cs           (Category="SMSPROVIDER")
# edits src/SHFramework.slnx to include the new project (alphabetical, idempotent).

shf make:provider Sms --first-driver Twilio
# everything above, plus:
#   src/Providers.Sms/Twilio/TwilioProvider.cs           (implements ISmsProvider, primary ctor)
# - SmsProviderType.Twilio = 0 added to the enum
# - factory switch wired: SmsProviderType.Twilio => new TwilioProvider(credential)
```

After the files land, register the provider in `Program.cs`:

```csharp
builder.Services.AddSmsProvider();
```

### Flags

| Flag | Default | Description |
|---|---|---|
| `--first-driver <Driver>` | — | Seed one driver immediately. Adds it to the `ProviderType` enum (= 0) and to the factory switch. |
| `--force` | false | Overwrite existing files. |
| `--dry-run` | false | Print the file list without touching disk. |
| `--project <path>` | auto | Override the `Business` project location. Other paths (`Providers.<Name>/`, `SHFramework.slnx`) are derived from it. |

## `make:provider-driver`

Add a driver to a provider that was already scaffolded with `make:provider`. Three operations, all idempotent on re-run:

```bash
shf make:provider-driver Sms Twilio
# writes  src/Providers.Sms/Twilio/TwilioProvider.cs
# edits   src/Business/Providers/Sms/SmsProviderType.cs   (adds Twilio = 0,)
# edits   src/Providers.Sms/ProviderFactory.cs            (adds switch case + using import)

shf make:provider-driver Sms SendGrid
# adds SendGrid = 1, SmsProviderType.SendGrid => new SendGridProvider(credential), and the using
```

### Flags

| Flag | Default | Description |
|---|---|---|
| `--force` | false | Overwrite the driver file if it already exists. (Enum + factory edits are idempotent — `--force` doesn't change their behavior.) |
| `--dry-run` | false | Print what would happen without touching disk. |
| `--project <path>` | auto | Override the `Business` project location. |

The command refuses to run when `Providers.<Provider>/Providers.<Provider>.csproj`, `Business/Providers/<Provider>/<Provider>ProviderType.cs`, or `Providers.<Provider>/ProviderFactory.cs` is missing — run `make:provider` first.

## `make:persistence`

Scaffolds an EF Core persistence project. Three variants — same shape, different EF provider.

```bash
shf make:persistence postgres
# writes:
#   src/Persistence.PostgreSql/Persistence.PostgreSql.csproj
#   src/Persistence.PostgreSql/PostgreSqlDbContext.cs               (ApplyConfigurationsFromAssembly)
#   src/Persistence.PostgreSql/PostgreSqlDbContextFactory.cs        (IDesignTimeDbContextFactory<>)
#   src/Persistence.PostgreSql/PostgreSqlOptions.cs                 (ConnectionString)
#   src/Persistence.PostgreSql/RegisterPostgreSqlPersistence.cs     (DI extension)
# edits  src/SHFramework.slnx                                       (adds the project)
# edits  src/WebApi/appsettings.json + appsettings.Development.json (adds "Persistence" section)

shf make:persistence sqlserver --localdb
# default conn: Server=(localdb)\mssqllocaldb;Database=AppDb;Trusted_Connection=true;

shf make:persistence sqlite --connection-string "Data Source=cache.db"
```

After the files land, register the persistence in `Program.cs`:

```csharp
builder.Services.AddPostgreSqlPersistence(builder.Configuration);
```

### Design-time tooling

The generated `<Variant>DbContextFactory` reads `PERSISTENCE_CONNECTION_STRING` (env var) or falls back to a compiled-in default. `dotnet ef migrations add ...` works from any working directory without dragging in `appsettings` loading; set the env var if your default isn't right.

### Variants

| Argument | Project name | Default connection string | EF package |
|---|---|---|---|
| `postgres` | `Persistence.PostgreSql` | `Host=localhost;Port=5432;Database=AppDb;Username=postgres;` | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| `sqlserver` | `Persistence.SqlServer` | `Server=localhost,1433;Database=AppDb;Trusted_Connection=true;TrustServerCertificate=true;` | `Microsoft.EntityFrameworkCore.SqlServer` |
| `sqlserver --localdb` | `Persistence.SqlServer` | `Server=(localdb)\mssqllocaldb;Database=AppDb;Trusted_Connection=true;` | `Microsoft.EntityFrameworkCore.SqlServer` |
| `sqlite` | `Persistence.Sqlite` | `Data Source=app.db` | `Microsoft.EntityFrameworkCore.Sqlite` |

### Flags

| Flag | Default | Description |
|---|---|---|
| `--connection-string <conn>` | variant default (or LocalDB if `--localdb`) | Override the connection string written to appsettings and used as the design-time factory's default. |
| `--localdb` | false | sqlserver only — use the LocalDB default. Ignored for other variants. |
| `--force` | false | Overwrite existing files. |
| `--dry-run` | false | Print what would happen without touching disk. |
| `--project <path>` | auto | Override the `Business` project location. |

The `appsettings` edit is idempotent — re-running won't clobber a user-customized `Persistence` section.

⚠️ **Secrets:** if your connection string contains a password, put it in user-secrets, not `appsettings.json`. See [`docs/SECRETS.md`](https://github.com/StrawhatsCompany/sh-framework-template/blob/main/docs/SECRETS.md) in the framework repo.

## `make:migration`

Thin wrapper around `dotnet ef migrations add` that auto-detects the persistence project and the WebApi startup project. Saves you the four `--project` / `--startup-project` / `--output-dir` flags every time.

```bash
shf make:migration AddForecastTable
# Equivalent to:
# dotnet ef migrations add AddForecastTable \
#     --project src/Persistence.PostgreSql/Persistence.PostgreSql.csproj \
#     --startup-project src/WebApi/WebApi.csproj \
#     --output-dir Migrations
```

### Multiple persistences

If you have more than one `Persistence.*` project under `src/`, the command refuses to guess — pass `--persistence`:

```bash
shf make:migration RenameUserEmail --persistence Persistence.Sqlite
```

### Flags

| Flag | Default | Description |
|---|---|---|
| `--persistence <name>` | auto when there is exactly one `Persistence.*` project | Project to add the migration to. |
| `--output-dir <dir>` | `Migrations` | Folder for the generated migration files, relative to the persistence project. |
| `--dry-run` | false | Print the planned `dotnet ef` invocation without running it. |
| `--project <path>` | auto | Override the `Business` project location. |

Requires `dotnet-ef` to be installed (`dotnet tool install -g dotnet-ef`).

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
