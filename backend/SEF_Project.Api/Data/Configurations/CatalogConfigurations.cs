using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Category { Id = SeedData.CategoryTops, Name = "Tops", IsActive = true },
            new Category { Id = SeedData.CategoryBottoms, Name = "Bottoms", IsActive = true },
            new Category { Id = SeedData.CategoryOuterwear, Name = "Outerwear", IsActive = true },
            new Category { Id = SeedData.CategoryFootwear, Name = "Footwear", IsActive = true });
    }
}

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("Collections");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Collection
            {
                Id = SeedData.CollectionSummerEssentials,
                Name = "Summer Essentials",
                Description = "Lightweight staples for the warm season.",
                IsActive = true
            },
            new Collection
            {
                Id = SeedData.CollectionSignature,
                Name = "Signature Selection",
                Description = "Featured pieces from the signature line.",
                IsActive = true
            },
            new Collection
            {
                Id = SeedData.CollectionNewArrivals,
                Name = "New Arrivals",
                Description = "The latest additions to the catalogue.",
                IsActive = true
            });
    }
}

public class SizeConfiguration : IEntityTypeConfiguration<Size>
{
    public void Configure(EntityTypeBuilder<Size> builder)
    {
        builder.ToTable("Sizes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Size { Id = SeedData.SizeXs, Name = "XS", DisplayOrder = 10, IsActive = true },
            new Size { Id = SeedData.SizeS, Name = "S", DisplayOrder = 20, IsActive = true },
            new Size { Id = SeedData.SizeM, Name = "M", DisplayOrder = 30, IsActive = true },
            new Size { Id = SeedData.SizeL, Name = "L", DisplayOrder = 40, IsActive = true },
            new Size { Id = SeedData.SizeXl, Name = "XL", DisplayOrder = 50, IsActive = true },
            new Size { Id = SeedData.SizeOneSize, Name = "One Size", DisplayOrder = 60, IsActive = true });
    }
}

public class ColourConfiguration : IEntityTypeConfiguration<Colour>
{
    public void Configure(EntityTypeBuilder<Colour> builder)
    {
        builder.ToTable("Colours", table =>
        {
            table.HasCheckConstraint(
                "CK_Colours_HexCode_Format",
                "\"HexCode\" IS NULL OR (length(\"HexCode\") = 7 AND substr(\"HexCode\", 1, 1) = '#')");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.HexCode).HasMaxLength(7);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Colour { Id = SeedData.ColourBlack, Name = "Black", HexCode = "#000000", IsActive = true },
            new Colour { Id = SeedData.ColourWhite, Name = "White", HexCode = "#ffffff", IsActive = true },
            new Colour { Id = SeedData.ColourNavy, Name = "Navy", HexCode = "#001f3f", IsActive = true });
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);

        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.CollectionId);

        builder.HasOne(e => e.Category)
            .WithMany(e => e.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Collection)
            .WithMany(e => e.Products)
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Supplier)
            .WithMany(e => e.Products)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new Product
            {
                Id = SeedData.ProductTShirt,
                Name = "Classic Cotton T-Shirt",
                Description = "Soft combed cotton crew-neck tee.",
                CategoryId = SeedData.CategoryTops,
                CollectionId = SeedData.CollectionSummerEssentials,
                SupplierId = SeedData.SupplierAtlasTextiles,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductHoodie,
                Name = "Fleece Pullover Hoodie",
                Description = "Brushed fleece hoodie with a kangaroo pocket.",
                CategoryId = SeedData.CategoryTops,
                CollectionId = SeedData.CollectionNewArrivals,
                SupplierId = SeedData.SupplierAtlasTextiles,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductJeans,
                Name = "Slim Fit Denim Jeans",
                Description = "Mid-rise slim jeans in stretch denim.",
                CategoryId = SeedData.CategoryBottoms,
                CollectionId = SeedData.CollectionSummerEssentials,
                SupplierId = SeedData.SupplierAtlasTextiles,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductJacket,
                Name = "Quilted Field Jacket",
                Description = "Lightly quilted jacket for layering.",
                CategoryId = SeedData.CategoryOuterwear,
                CollectionId = SeedData.CollectionSignature,
                SupplierId = SeedData.SupplierAtlasTextiles,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductBoots,
                Name = "Leather Ankle Boots",
                Description = "Full-grain leather boots with a block heel.",
                CategoryId = SeedData.CategoryFootwear,
                CollectionId = SeedData.CollectionSignature,
                SupplierId = SeedData.SupplierNordicFootwear,
                IsActive = true
            });
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants", table =>
        {
            table.HasCheckConstraint("CK_ProductVariants_Price", "\"Price\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Sku).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Price).HasPrecision(18, 2);

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.Sku).IsUnique();
        builder.HasIndex(e => new { e.ProductId, e.SizeId, e.ColourId }).IsUnique();

        builder.HasOne(e => e.Product)
            .WithMany(e => e.Variants)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Size)
            .WithMany(e => e.ProductVariants)
            .HasForeignKey(e => e.SizeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Colour)
            .WithMany(e => e.ProductVariants)
            .HasForeignKey(e => e.ColourId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new ProductVariant
            {
                Id = SeedData.VariantTShirtXs,
                ProductId = SeedData.ProductTShirt,
                SizeId = SeedData.SizeXs,
                ColourId = SeedData.ColourBlack,
                Sku = "TSH-CLS-XS",
                Name = "XS / Black",
                Price = 2500m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantTShirtM,
                ProductId = SeedData.ProductTShirt,
                SizeId = SeedData.SizeM,
                ColourId = SeedData.ColourWhite,
                Sku = "TSH-CLS-M",
                Name = "M / White",
                Price = 2600m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantHoodieM,
                ProductId = SeedData.ProductHoodie,
                SizeId = SeedData.SizeM,
                ColourId = SeedData.ColourNavy,
                Sku = "HOD-FLC-M",
                Name = "M / Navy",
                Price = 6500m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantHoodieL,
                ProductId = SeedData.ProductHoodie,
                SizeId = SeedData.SizeL,
                ColourId = SeedData.ColourNavy,
                Sku = "HOD-FLC-L",
                Name = "L / Navy",
                Price = 6900m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantJeansM,
                ProductId = SeedData.ProductJeans,
                SizeId = SeedData.SizeM,
                ColourId = SeedData.ColourBlack,
                Sku = "JEA-SLM-M",
                Name = "M / Black",
                Price = 7500m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantJacketL,
                ProductId = SeedData.ProductJacket,
                SizeId = SeedData.SizeL,
                ColourId = SeedData.ColourNavy,
                Sku = "JKT-QFD-L",
                Name = "L / Navy",
                Price = 12500m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantBootsOneSize,
                ProductId = SeedData.ProductBoots,
                SizeId = SeedData.SizeOneSize,
                ColourId = SeedData.ColourBlack,
                Sku = "BTS-ANK-OS",
                Name = "One Size / Black",
                Price = 8900m,
                IsActive = true
            });
    }
}

public class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.ToTable("InventoryStocks", table =>
        {
            table.HasCheckConstraint("CK_InventoryStocks_QuantityOnHand", "\"QuantityOnHand\" >= 0");
            table.HasCheckConstraint("CK_InventoryStocks_ReservedQuantity", "\"ReservedQuantity\" >= 0");
            table.HasCheckConstraint("CK_InventoryStocks_ReorderLevel", "\"ReorderLevel\" >= 0");
            table.HasCheckConstraint("CK_InventoryStocks_AvailableQuantity", "\"QuantityOnHand\" >= \"ReservedQuantity\"");
        });

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.ProductVariantId).IsUnique();

        builder.HasOne(e => e.ProductVariant)
            .WithOne(e => e.InventoryStock)
            .HasForeignKey<InventoryStock>(e => e.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000041"),
                ProductVariantId = SeedData.VariantTShirtXs,
                QuantityOnHand = 50,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000042"),
                ProductVariantId = SeedData.VariantTShirtM,
                QuantityOnHand = 30,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000043"),
                ProductVariantId = SeedData.VariantHoodieM,
                QuantityOnHand = 40,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000044"),
                ProductVariantId = SeedData.VariantHoodieL,
                QuantityOnHand = 25,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000045"),
                ProductVariantId = SeedData.VariantJeansM,
                QuantityOnHand = 35,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000046"),
                ProductVariantId = SeedData.VariantJacketL,
                QuantityOnHand = 200,
                ReservedQuantity = 0,
                ReorderLevel = 50
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000047"),
                ProductVariantId = SeedData.VariantBootsOneSize,
                QuantityOnHand = 20,
                ReservedQuantity = 0,
                ReorderLevel = 5
            });
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions", table =>
        {
            table.HasCheckConstraint("CK_StockTransactions_QuantityChange", "\"QuantityChange\" <> 0");
            table.HasCheckConstraint("CK_StockTransactions_QuantityOnHandBefore", "\"QuantityOnHandBefore\" >= 0");
            table.HasCheckConstraint("CK_StockTransactions_QuantityOnHandAfter", "\"QuantityOnHandAfter\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasIndex(e => e.ProductVariantId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.PerformedByUserId);

        builder.HasOne(e => e.ProductVariant)
            .WithMany(e => e.StockTransactions)
            .HasForeignKey(e => e.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PerformedByUser)
            .WithMany()
            .HasForeignKey(e => e.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ContactName).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.Phone).HasMaxLength(50);

        builder.HasData(
            new Supplier
            {
                Id = SeedData.SupplierAtlasTextiles,
                Name = "Atlas Textiles",
                ContactName = "Nimal Perera",
                Email = "orders@atlastextiles.lk",
                Phone = "+94 11 234 5678",
                IsActive = true
            },
            new Supplier
            {
                Id = SeedData.SupplierNordicFootwear,
                Name = "Nordic Footwear",
                ContactName = "Kamal Silva",
                Email = "sales@nordicfootwear.lk",
                Phone = "+94 11 876 5432",
                IsActive = true
            });
    }
}
