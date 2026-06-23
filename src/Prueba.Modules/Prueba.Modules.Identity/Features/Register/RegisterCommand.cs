namespace Prueba.Modules.Identity.Features.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName);
