using DineOS.Application.Interfaces.Services;
using RazorLight;

namespace DineOS.Infrastructure.Services;

/// <summary>
/// Renders email templates from Razor (.cshtml) files located under
/// <c>EmailTemplates/</c> beside the running assembly. Templates are cached
/// after first compile by RazorLight itself.
/// </summary>
public sealed class RazorLightEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly RazorLightEngine _engine;

    public RazorLightEmailTemplateRenderer()
    {
        var templateRoot = Path.Combine(AppContext.BaseDirectory, "EmailTemplates");
        _engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(templateRoot)
            .UseMemoryCachingProvider()
            .Build();
    }

    public Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken ct = default)
    {
        var file = templateName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : $"{templateName}.cshtml";
        return _engine.CompileRenderAsync(file, model);
    }
}
