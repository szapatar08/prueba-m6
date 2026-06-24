namespace Prueba.Application.Interfaces;

public interface IEmailTemplateRenderer
{
    (string Subject, string Body) Render(string templateType, object data);
    Task<(string Subject, string Body)> RenderAsync(string templateType, object data, CancellationToken cancellationToken = default);
}
