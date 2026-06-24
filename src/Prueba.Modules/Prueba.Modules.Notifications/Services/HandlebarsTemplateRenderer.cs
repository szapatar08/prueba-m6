using HandlebarsDotNet;
using Microsoft.Extensions.Caching.Memory;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.Modules.Notifications.Services;

public class HandlebarsTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IMemoryCache _cache;

    public HandlebarsTemplateRenderer(
        IRepository repository,
        ICurrentTenant currentTenant,
        IMemoryCache cache)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _cache = cache;
    }

    public (string Subject, string Body) Render(string templateType, object data)
    {
        var template = LoadTemplate(templateType);
        var subject = CompileAndRender(template.SubjectTemplate, data);
        var body = CompileAndRender(template.BodyTemplate, data);
        return (subject, body);
    }

    public Task<(string Subject, string Body)> RenderAsync(string templateType, object data, CancellationToken cancellationToken = default)
    {
        var result = Render(templateType, data);
        return Task.FromResult(result);
    }

    private NotificationTemplate LoadTemplate(string templateType)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;
        var cacheKey = $"template:{tenantId}:{templateType}";

        if (_cache.TryGetValue(cacheKey, out NotificationTemplate? cached) && cached is not null)
        {
            return cached;
        }

        var template = _repository.Query<NotificationTemplate>()
            .FirstOrDefault(t => t.Type == templateType && t.TenantId == tenantId)
            ?? throw new TemplateNotFoundException(templateType);

        _cache.Set(cacheKey, template, TimeSpan.FromHours(24));
        return template;
    }

    private static string CompileAndRender(string templateSource, object data)
    {
        try
        {
            var compiled = Handlebars.Compile(templateSource);
            return compiled(data);
        }
        catch (Exception ex) when (ex is not TemplateNotFoundException)
        {
            throw new TemplateCompilationException("unknown", ex);
        }
    }
}
