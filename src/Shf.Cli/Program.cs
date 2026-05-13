using Microsoft.Extensions.DependencyInjection;
using Shf.Cli.Commands.List;
using Shf.Cli.Commands.Make;
using Shf.Cli.Infrastructure;
using Shf.Cli.Services;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton<IProjectLocator, ProjectLocator>();
services.AddSingleton<ITemplateRenderer, TokenTemplateRenderer>();
services.AddSingleton<IFileWriter, FileWriter>();
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<IEndpointScanner, EndpointScanner>();
services.AddSingleton<IAuthTemplateLoader, AuthTemplateLoader>();
services.AddSingleton<IGitHubIssueClient, GitHubIssueClient>();

var app = new CommandApp(new TypeRegistrar(services));

app.Configure(config =>
{
    config.SetApplicationName("shf");
    config.AddCommand<MakeFeatureCommand>("make:feature")
        .WithDescription("Scaffold a CQRS feature (request, handler, optional response).")
        .WithExample("make:feature", "Weather/GetForecastsByCity")
        .WithExample("make:feature", "Mails/SendMail", "--command");

    config.AddCommand<MakeEndpointCommand>("make:endpoint")
        .WithDescription("Scaffold a minimal API endpoint.");
    config.AddCommand<MakeEntityCommand>("make:entity")
        .WithDescription("Scaffold a Domain entity.");
    config.AddCommand<MakeProviderCommand>("make:provider")
        .WithDescription("Scaffold a provider contract + project skeleton.");
    config.AddCommand<MakeProviderDriverCommand>("make:provider-driver")
        .WithDescription("Add a new driver to an existing provider.");
    config.AddCommand<MakePersistenceCommand>("make:persistence")
        .WithDescription("Scaffold a persistence project (PostgreSQL / SQL Server / SQLite).");
    config.AddCommand<MakeCachingCommand>("make:caching")
        .WithDescription("Scaffold a caching provider project (Caching.<Name>) wired to ICacheProvider.");
    config.AddCommand<MakeMigrationCommand>("make:migration")
        .WithDescription("Add a design-time EF Core migration to a persistence project.");
    config.AddCommand<MakeAuthenticationCommand>("make:authentication")
        .WithDescription("Plan an authentication implementation — interactive picker (or --types csv), opens GitHub issues per selected feature with implementation guidance against the sh-framework-template reference.")
        .WithExample("make:authentication")
        .WithExample("make:authentication", "--types", "jwt,refresh,apikey", "--no-tenant")
        .WithExample("make:authentication", "--types", "all", "--tenant", "--dry-run");

    config.AddCommand<ListEndpointsCommand>("list:endpoints")
        .WithDescription("List all IEndpoint implementations in the WebApi project as a table.")
        .WithExample("list:endpoints")
        .WithExample("list:endpoints", "--filter", "weather")
        .WithExample("list:endpoints", "--json");
});

return await app.RunAsync(args);
