using FluentValidation;

namespace Prueba.Modules.Properties.Features.CreateProperty;

public class CreatePropertyValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(200).WithMessage("City must not exceed 200 characters");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(200).WithMessage("Country must not exceed 200 characters");

        RuleFor(x => x.PricePerNight)
            .GreaterThan(0).WithMessage("Price per night must be positive");

        RuleFor(x => x.MaxGuests)
            .GreaterThan(0).WithMessage("Max guests must be positive");

        RuleFor(x => x.Bedrooms)
            .GreaterThanOrEqualTo(0).WithMessage("Bedrooms cannot be negative");

        RuleFor(x => x.Bathrooms)
            .GreaterThanOrEqualTo(0).WithMessage("Bathrooms cannot be negative");
    }
}
