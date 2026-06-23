using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Reports.Features.GenerateReport;

public class GenerateReportHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GenerateReportHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<byte[]>> Handle(
        GenerateReportQuery query,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Get booking data
        var bookings = await GetBookingsAsync(query, ownerId, tenantId, cancellationToken);

        if (bookings.Count == 0)
            return Result<byte[]>.Fail("No bookings found for the specified criteria.");

        // Generate Excel report
        var excelBytes = GenerateExcelReport(bookings, query);

        return Result<byte[]>.Success(excelBytes);
    }

    private async Task<List<ReportBookingData>> GetBookingsAsync(
        GenerateReportQuery query,
        Guid ownerId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Get owner's properties
        var propertiesQuery = _repository.Query<Prueba.Modules.Properties.Entities.Property>()
            .IgnoreQueryFilters()
            .Where(p => p.OwnerId == ownerId && p.TenantId == tenantId);

        if (query.PropertyId.HasValue)
            propertiesQuery = propertiesQuery.Where(p => p.Id == query.PropertyId.Value);

        var properties = await propertiesQuery.ToListAsync(cancellationToken);
        var propertyIds = properties.Select(p => p.Id).ToList();

        // Get bookings for these properties
        var bookings = await _repository.Query<BookingEntity>()
            .IgnoreQueryFilters()
            .Where(b => propertyIds.Contains(b.PropertyId)
                && b.TenantId == tenantId
                && b.Status == BookingStatus.Confirmed
                && b.StartDate < query.EndDate
                && b.EndDate > query.StartDate)
            .ToListAsync(cancellationToken);

        // Get guest emails
        var guestIds = bookings.Select(b => b.GuestId).Distinct().ToList();
        var guests = await _repository.Query<Prueba.Modules.Identity.Entities.User>()
            .IgnoreQueryFilters()
            .Where(u => guestIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        // Combine data
        var result = new List<ReportBookingData>();
        foreach (var booking in bookings)
        {
            var property = properties.First(p => p.Id == booking.PropertyId);
            var guestEmail = guests.GetValueOrDefault(booking.GuestId, "Unknown");

            result.Add(new ReportBookingData(
                property.Name,
                property.Location,
                booking.StartDate,
                booking.EndDate,
                booking.TotalPrice,
                guestEmail,
                booking.CreatedAt));
        }

        return result.OrderBy(r => r.StartDate).ToList();
    }

    private static byte[] GenerateExcelReport(List<ReportBookingData> bookings, GenerateReportQuery query)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Bookings Report");

        // Title
        worksheet.Cell("A1").Value = "Bookings Report";
        worksheet.Cell("A1").Style.Font.Bold = true;
        worksheet.Cell("A1").Style.Font.FontSize = 16;

        // Date range
        worksheet.Cell("A2").Value = $"Period: {query.StartDate:yyyy-MM-dd} to {query.EndDate:yyyy-MM-dd}";
        worksheet.Cell("A2").Style.Font.Italic = true;

        // Headers
        var headers = new[] { "Property", "Location", "Check-in", "Check-out", "Price", "Guest Email", "Booking Date" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(4, i + 1).Value = headers[i];
            worksheet.Cell(4, i + 1).Style.Font.Bold = true;
            worksheet.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Data
        for (int i = 0; i < bookings.Count; i++)
        {
            var row = i + 5;
            var booking = bookings[i];

            worksheet.Cell(row, 1).Value = booking.PropertyName;
            worksheet.Cell(row, 2).Value = booking.PropertyLocation;
            worksheet.Cell(row, 3).Value = booking.StartDate.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 4).Value = booking.EndDate.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 5).Value = booking.TotalPrice;
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(row, 6).Value = booking.GuestEmail;
            worksheet.Cell(row, 7).Value = booking.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        }

        // Summary
        var summaryRow = bookings.Count + 6;
        worksheet.Cell(summaryRow, 1).Value = "Total Bookings:";
        worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;
        worksheet.Cell(summaryRow, 2).Value = bookings.Count;

        worksheet.Cell(summaryRow + 1, 1).Value = "Total Revenue:";
        worksheet.Cell(summaryRow + 1, 1).Style.Font.Bold = true;
        worksheet.Cell(summaryRow + 1, 2).Value = bookings.Sum(b => b.TotalPrice);
        worksheet.Cell(summaryRow + 1, 2).Style.NumberFormat.Format = "$#,##0.00";

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
