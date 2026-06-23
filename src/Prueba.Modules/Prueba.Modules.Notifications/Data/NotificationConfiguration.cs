using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.Modules.Notifications.Data;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>, IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired();

        builder.Property(n => n.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.SentAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.TenantId);
        builder.HasIndex(n => new { n.UserId, n.TenantId });
        builder.HasIndex(n => n.IsRead);
        builder.HasIndex(n => n.SentAt);
    }

    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.SubjectTemplate)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.BodyTemplate)
            .IsRequired()
            .HasMaxLength(10000);

        // Indexes
        builder.HasIndex(t => t.Type);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => new { t.Type, t.TenantId }).IsUnique();
    }
}
