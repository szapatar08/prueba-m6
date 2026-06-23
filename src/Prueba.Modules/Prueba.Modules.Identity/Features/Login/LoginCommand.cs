namespace Prueba.Modules.Identity.Features.Login;

public record LoginCommand(
    string Email,
    string Password);
