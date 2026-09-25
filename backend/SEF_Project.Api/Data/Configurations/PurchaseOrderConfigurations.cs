using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Data.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(e => e.OrderNumber).IsUnique();
        builder.HasIndex(e => new { e.SupplierId, e.Status });

        builder.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems", table =>
        {
            table.HasCheckConstraint(
                "CK_PurchaseOrderItems_Quantity",
                "\"Quantity\" > 0");
            table.HasCheckConstraint(
                "CK_PurchaseOrderItems_UnitCost",
                "\"UnitCost\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UnitCost).HasPrecision(18, 2);

        builder.HasIndex(e => e.PurchaseOrderId);
        builder.HasIndex(e => e.ProductVariantId);

        builder.HasOne(e => e.PurchaseOrder)
            .WithMany(order => order.Items)
            .HasForeignKey(e => e.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ProductVariant)
            .WithMany()
            .HasForeignKey(e => e.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
