namespace DineOS.Application.Interfaces.Services;

public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders a Razor email template by logical name (e.g. "AccountVerification").
    /// The implementation locates the template file under a configured root.
    /// </summary>
    Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken ct = default);
}
