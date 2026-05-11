namespace Shf.Cli.Services;

public interface ITemplateRenderer
{
    string Render(string templatePath, object model);
}
