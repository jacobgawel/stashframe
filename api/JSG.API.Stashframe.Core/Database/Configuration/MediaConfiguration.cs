using JSG.API.Stashframe.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JSG.API.Stashframe.Core.Database.Configuration;

internal class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("media");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .HasMaxLength(100);
        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.MediaStatus)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(e => e.OriginalMimeType)
            .IsRequired();
        builder.Property(e => e.OriginalSizeBytes)
            .IsRequired();
        builder.Property(e => e.OriginalFilename)
            .IsRequired();

        builder.HasMany(m => m.ShareLinks)
            .WithOne(sl => sl.Media)
            .HasForeignKey(sl => sl.MediaId);

    }
}
