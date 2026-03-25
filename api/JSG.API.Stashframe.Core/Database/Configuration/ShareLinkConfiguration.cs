using JSG.API.Stashframe.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JSG.API.Stashframe.Core.Database.Configuration;

public class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        builder.ToTable("share_links");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Slug)
            .IsRequired();

        builder.Property(e => e.Visibility)
            .HasConversion<string>()
            .IsRequired();

        builder.HasOne(sl => sl.Media)
            .WithMany(m => m.ShareLinks)
            .HasForeignKey(sl => sl.MediaId);
    }
}
