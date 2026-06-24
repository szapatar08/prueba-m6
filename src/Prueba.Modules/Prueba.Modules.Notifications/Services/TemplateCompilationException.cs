namespace Prueba.Modules.Notifications.Services;

public class TemplateCompilationException : Exception
{
    public TemplateCompilationException(string templateType, Exception innerException)
        : base($"Failed to compile template for type '{templateType}'.", innerException)
    {
    }
}
