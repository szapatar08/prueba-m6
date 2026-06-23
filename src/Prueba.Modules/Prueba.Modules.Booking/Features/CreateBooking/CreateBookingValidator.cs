using FluentValidation;

namespace Prueba.Modules.Booking.Features.CreateBooking;

public class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.PropertyId)
            .NotEmpty().WithMessage("Property ID is required.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.TotalPrice)
            .GreaterThan(0).WithMessage("Total price must be positive.");
    }
}
