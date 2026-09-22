using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.Orders;

namespace SEF_Project.Api.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Subtotal", "\"Subtotal\" >= 0");
            table.HasCheckConstraint("CK_Orders_DiscountTotal", "\"DiscountTotal\" >= 0");
            table.HasCheckConstraint("CK_Orders_TaxAmount", "\"TaxAmount\" >= 0");
            table.HasCheckConstraint("CK_Orders_ShippingFee", "\"ShippingFee\" >= 0");
            table.HasCheckConstraint("CK_Orders_Total", "\"Total\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Subtotal).HasPrecision(18, 2);
        builder.Property(e => e.DiscountTotal).HasPrecision(18, 2);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.ShippingFee).HasPrecision(18, 2);
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);

        builder.HasIndex(e => e.OrderNumber).IsUnique();
        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_OrderItems_UnitPrice", "\"UnitPrice\" > 0");
            table.HasCheckConstraint("CK_OrderItems_LineTotal", "\"LineTotal\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.LineTotal).HasPrecision(18, 2);

        builder.HasIndex(e => e.ProductVariantId);

        builder.HasOne(e => e.Order)
            .WithMany(e => e.Items)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ProductVariant)
            .WithMany()
            .HasForeignKey(e => e.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderAddressConfiguration : IEntityTypeConfiguration<OrderAddress>
{
    public void Configure(EntityTypeBuilder<OrderAddress> builder)
    {
        builder.ToTable("OrderAddresses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Line1).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Line2).HasMaxLength(200);
        builder.Property(e => e.City).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Province).HasMaxLength(100);
        builder.Property(e => e.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Country).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(50);

        builder.HasIndex(e => e.OrderId).IsUnique();

        builder.HasOne(e => e.Order)
            .WithOne(e => e.DeliveryAddress)
            .HasForeignKey<OrderAddress>(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.HasIndex(e => new { e.OrderId, e.ChangedAt });

        builder.HasOne(e => e.Order)
            .WithMany(e => e.StatusHistory)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ChangedByUser)
            .WithMany()
            .HasForeignKey(e => e.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Method).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.TransactionReference).HasMaxLength(200);

        builder.HasIndex(e => e.OrderId);

        builder.HasOne(e => e.Order)
            .WithMany(e => e.Payments)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.TrackingNumber).HasMaxLength(100);
        builder.Property(e => e.Carrier).HasMaxLength(100);

        builder.HasIndex(e => e.OrderId);

        builder.HasOne(e => e.Order)
            .WithMany(e => e.Shipments)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
