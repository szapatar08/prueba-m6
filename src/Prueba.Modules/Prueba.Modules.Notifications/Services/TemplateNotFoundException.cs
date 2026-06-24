namespace Prueba.Modules.Notifications.Services;

public class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(string templateType)
        : base($"Template not found for type '{templateType}'.")
    {
    }

    public TemplateNotFoundException(string templateType, Exception innerException)
        : base($"Template not found for type '{templateType}'.", innerException)
    {
    }
}
