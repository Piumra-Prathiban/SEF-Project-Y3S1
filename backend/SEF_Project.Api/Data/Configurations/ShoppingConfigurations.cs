using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.Shopping;

namespace SEF_Project.Api.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.CustomerId).IsUnique();

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", table =>
        {
            table.HasCheckConstraint("CK_CartItems_Quantity", "\"Quantity\" > 0");
        });

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.CartId, e.ProductVariantId }).IsUnique();

        builder.HasOne(e => e.Cart)
            .WithMany(e => e.Items)
            .HasForeignKey(e => e.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ProductVariant)
            .WithMany()
            .HasForeignKey(e => e.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
