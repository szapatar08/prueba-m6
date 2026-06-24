using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Infrastructure.Data;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.KYC.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.Api;

public static class SeedData
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid GuestUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        // Force load module assemblies so EF Core discovers all entity types
        ForceLoadModuleAssemblies();

        logger.LogInformation("Applying migrations...");
        await context.Database.MigrateAsync();

        logger.LogInformation("Seeding data...");
        await SeedAsync(context, logger);
        logger.LogInformation("Seeding complete.");
    }

    private static void ForceLoadModuleAssemblies()
    {
        _ = typeof(Modules.Identity.Entities.User).Assembly;
        _ = typeof(Modules.Properties.Entities.Property).Assembly;
        _ = typeof(Modules.Booking.Entities.BookingEntity).Assembly;
        _ = typeof(Modules.Wishlist.Entities.WishlistItem).Assembly;
        _ = typeof(Modules.KYC.Entities.KycValidation).Assembly;
        _ = typeof(Modules.Notifications.Entities.Notification).Assembly;
        _ = typeof(Modules.Dashboard.Features.GetOccupancyRate.GetOccupancyRateQuery).Assembly;
        _ = typeof(Modules.Reports.Features.GenerateReport.GenerateReportQuery).Assembly;
    }

    private static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        // Seed Roles
        if (!await context.Set<Role>().IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seeding roles...");
            var roles = new[]
            {
                Role.Create("Admin", DefaultTenantId),
                Role.Create("Owner", DefaultTenantId),
                Role.Create("Guest", DefaultTenantId),
            };
            await context.Set<Role>().AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // Seed Users
        if (!await context.Set<User>().IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seeding users...");
            var adminRole = await context.Set<Role>().IgnoreQueryFilters().FirstAsync(r => r.Name == "Admin");
            var ownerRole = await context.Set<Role>().IgnoreQueryFilters().FirstAsync(r => r.Name == "Owner");
            var guestRole = await context.Set<Role>().IgnoreQueryFilters().FirstAsync(r => r.Name == "Guest");

            var admin = User.Create("admin@prueba.com", BCrypt.Net.BCrypt.HashPassword("Admin123!"), "Admin", "User", DefaultTenantId);
            var owner = User.Create("owner@prueba.com", BCrypt.Net.BCrypt.HashPassword("Owner123!"), "Owner", "User", DefaultTenantId);
            var guest = User.Create("guest@prueba.com", BCrypt.Net.BCrypt.HashPassword("Guest123!"), "Guest", "User", DefaultTenantId);

            await context.Set<User>().AddRangeAsync(admin, owner, guest);
            await context.SaveChangesAsync();

            // Assign roles
            await context.Set<UserRole>().AddRangeAsync(
                UserRole.Create(admin.Id, adminRole.Id, DefaultTenantId),
                UserRole.Create(owner.Id, ownerRole.Id, DefaultTenantId),
                UserRole.Create(guest.Id, guestRole.Id, DefaultTenantId)
            );
            await context.SaveChangesAsync();
        }

        // Seed Properties
        if (!await context.Set<Property>().IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seeding properties...");
            var owner = await context.Set<User>().IgnoreQueryFilters().FirstAsync(u => u.Email == "owner@prueba.com");

            var properties = new[]
            {
                Property.Create("Casa en la playa", "Hermosa casa frente al mar con vista panorámica", "Frente al mar", "Av. Costera 123", "Cancún", "México", 150m, 6, 3, 2, owner.Id, DefaultTenantId),
                Property.Create("Departamento céntrico", "Moderno departamento en el corazón de la ciudad", "Centro", "Calle Principal 456", "Ciudad de México", "México", 80m, 2, 1, 1, owner.Id, DefaultTenantId),
                Property.Create("Cabaña en la montaña", "Acogedora cabaña rodeada de naturaleza", "Sierra", "Camino de la Montaña 789", "Tepoztlán", "México", 120m, 4, 2, 1, owner.Id, DefaultTenantId),
                Property.Create("Loft moderno", "Loft con diseño contemporáneo y terraza", "Roma Norte", "Calle Álvaro Obregón 321", "Ciudad de México", "México", 95m, 2, 1, 1, owner.Id, DefaultTenantId),
                Property.Create("Villa con alberca", "Villa de lujo con alberca privada y jardín", "Playa del Carmen", "Calle 10 Norte 654", "Playa del Carmen", "México", 250m, 8, 4, 3, owner.Id, DefaultTenantId),
            };

            await context.Set<Property>().AddRangeAsync(properties);
            await context.SaveChangesAsync();

            // Seed Availability (next 30 days)
            logger.LogInformation("Seeding availability...");
            var allProperties = await context.Set<Property>().IgnoreQueryFilters().ToListAsync();
            var availabilities = new List<Availability>();

            foreach (var property in allProperties)
            {
                for (int i = 1; i <= 30; i++)
                {
                    var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i));
                    availabilities.Add(Availability.Create(property.Id, date, true, property.PricePerNight, DefaultTenantId));
                }
            }

            await context.Set<Availability>().AddRangeAsync(availabilities);
            await context.SaveChangesAsync();
        }

        // Seed KYC for guest
        if (!await context.Set<KycValidation>().IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Seeding KYC validation...");
            var guest = await context.Set<User>().IgnoreQueryFilters().FirstAsync(u => u.Email == "guest@prueba.com");
            var kyc = KycValidation.Create(guest.Id, "DNI", DefaultTenantId);
            kyc.Approve("Guest User", "DOC123456", DateTime.SpecifyKind(new DateTime(1995, 5, 15), DateTimeKind.Utc), 0.95);
            await context.Set<KycValidation>().AddRangeAsync(kyc);
            await context.SaveChangesAsync();
        }
    }
}
