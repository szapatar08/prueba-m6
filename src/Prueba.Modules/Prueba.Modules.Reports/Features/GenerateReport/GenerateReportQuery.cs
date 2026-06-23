namespace Prueba.Modules.Reports.Features.GenerateReport;

public record GenerateReportQuery(
    Guid? PropertyId, // null = portfolio report
    DateOnly StartDate,
    DateOnly EndDate);

public record ReportBookingData(
    string PropertyName,
    string PropertyLocation,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalPrice,
    string GuestEmail,
    DateTime CreatedAt);
