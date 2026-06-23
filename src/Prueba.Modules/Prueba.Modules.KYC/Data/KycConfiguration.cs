using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Data;

public class KycConfiguration : IEntityTypeConfiguration<KycValidation>, IEntityTypeConfiguration<KycDocument>
{
    public void Configure(EntityTypeBuilder<KycValidation> builder)
    {
        builder.ToTable("KycValidations");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.UserId)
            .IsRequired();

        builder.Property(k => k.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(k => k.DocumentType)
            .HasMaxLength(50);

        builder.Property(k => k.ExtractedNames)
            .HasMaxLength(500);

        builder.Property(k => k.ExtractedDocumentNumber)
            .HasMaxLength(100);

        // Indexes for common queries
        builder.HasIndex(k => k.UserId);
        builder.HasIndex(k => k.TenantId);
        builder.HasIndex(k => k.Status);
        builder.HasIndex(k => new { k.UserId, k.TenantId });
    }

    public void Configure(EntityTypeBuilder<KycDocument> builder)
    {
        builder.ToTable("KycDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.KycValidationId)
            .IsRequired();

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(d => d.KycValidationId);
        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.UploadedAt);
    }
}
