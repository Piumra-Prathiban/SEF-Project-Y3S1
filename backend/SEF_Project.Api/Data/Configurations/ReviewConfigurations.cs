using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", table =>
        {
            table.HasCheckConstraint(
                "CK_Reviews_Rating",
                "\"Rating\" BETWEEN 1 AND 5");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Comment).HasMaxLength(2000);

        builder.HasIndex(e => new { e.ProductId, e.CustomerId }).IsUnique();
        builder.HasIndex(e => new { e.ProductId, e.IsPublished });

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
