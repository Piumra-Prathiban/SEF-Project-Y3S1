using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
            new Category { Id = SeedData.CategoryPizza, Name = "Pizza", IsActive = true },
            new Category { Id = SeedData.CategoryPasta, Name = "Pasta", IsActive = true },
            new Category { Id = SeedData.CategoryBeverages, Name = "Beverages", IsActive = true },
            new Category { Id = SeedData.CategoryDesserts, Name = "Desserts", IsActive = true });
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
                Id = SeedData.CollectionClassic,
                Name = "Classic Menu",
                Description = "Core menu items available every day.",
                IsActive = true
            },
            new Collection
            {
                Id = SeedData.CollectionSignature,
                Name = "Signature Meals",
                Description = "Featured meals and customer favourites.",
                IsActive = true
            },
            new Collection
            {
                Id = SeedData.CollectionDrinksAndDesserts,
                Name = "Drinks and Desserts",
                Description = "Beverages and sweet add-ons.",
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
            new Size { Id = SeedData.SizeSmall, Name = "Small", DisplayOrder = 10, IsActive = true },
            new Size { Id = SeedData.SizeMedium, Name = "Medium", DisplayOrder = 20, IsActive = true },
            new Size { Id = SeedData.SizeLarge, Name = "Large", DisplayOrder = 30, IsActive = true },
            new Size { Id = SeedData.SizeRegular, Name = "Regular", DisplayOrder = 40, IsActive = true },
            new Size { Id = SeedData.SizeSingle, Name = "Single", DisplayOrder = 50, IsActive = true },
            new Size { Id = SeedData.Size330Ml, Name = "330ml", DisplayOrder = 60, IsActive = true });
    }
}

public class ColourConfiguration : IEntityTypeConfiguration<Colour>
{
    public void Configure(EntityTypeBuilder<Colour> builder)
    {
        builder.ToTable("Colours");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.HexCode).HasMaxLength(7);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Colour { Id = SeedData.ColourDefault, Name = "Default", IsActive = true });
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
                Id = SeedData.ProductMargherita,
                Name = "Margherita Pizza",
                Description = "Classic tomato, mozzarella and basil.",
                CategoryId = SeedData.CategoryPizza,
                CollectionId = SeedData.CollectionClassic,
                SupplierId = SeedData.SupplierFreshFoods,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductPepperoni,
                Name = "Pepperoni Pizza",
                Description = "Pepperoni with mozzarella.",
                CategoryId = SeedData.CategoryPizza,
                CollectionId = SeedData.CollectionSignature,
                SupplierId = SeedData.SupplierFreshFoods,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductCarbonara,
                Name = "Spaghetti Carbonara",
                Description = "Creamy pasta with pancetta.",
                CategoryId = SeedData.CategoryPasta,
                CollectionId = SeedData.CollectionClassic,
                SupplierId = SeedData.SupplierFreshFoods,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductCola,
                Name = "Cola",
                Description = "Carbonated soft drink.",
                CategoryId = SeedData.CategoryBeverages,
                CollectionId = SeedData.CollectionDrinksAndDesserts,
                SupplierId = SeedData.SupplierBeverageCo,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductTiramisu,
                Name = "Tiramisu",
                Description = "Classic coffee dessert.",
                CategoryId = SeedData.CategoryDesserts,
                CollectionId = SeedData.CollectionDrinksAndDesserts,
                SupplierId = SeedData.SupplierFreshFoods,
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
            table.HasCheckConstraint("CK_ProductVariants_Price", "\"Price\" > 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Sku).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Price).HasPrecision(18, 2);

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
                Id = SeedData.VariantMargheritaSmall,
                ProductId = SeedData.ProductMargherita,
                SizeId = SeedData.SizeSmall,
                ColourId = SeedData.ColourDefault,
                Sku = "PIZ-MARG-S",
                Name = "Small / Default",
                Price = 1200m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantMargheritaLarge,
                ProductId = SeedData.ProductMargherita,
                SizeId = SeedData.SizeLarge,
                ColourId = SeedData.ColourDefault,
                Sku = "PIZ-MARG-L",
                Name = "Large / Default",
                Price = 2200m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantPepperoniMedium,
                ProductId = SeedData.ProductPepperoni,
                SizeId = SeedData.SizeMedium,
                ColourId = SeedData.ColourDefault,
                Sku = "PIZ-PEP-M",
                Name = "Medium / Default",
                Price = 1600m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantPepperoniLarge,
                ProductId = SeedData.ProductPepperoni,
                SizeId = SeedData.SizeLarge,
                ColourId = SeedData.ColourDefault,
                Sku = "PIZ-PEP-L",
                Name = "Large / Default",
                Price = 2600m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantCarbonaraRegular,
                ProductId = SeedData.ProductCarbonara,
                SizeId = SeedData.SizeRegular,
                ColourId = SeedData.ColourDefault,
                Sku = "PST-CARB-R",
                Name = "Regular / Default",
                Price = 1800m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantCola330,
                ProductId = SeedData.ProductCola,
                SizeId = SeedData.Size330Ml,
                ColourId = SeedData.ColourDefault,
                Sku = "BEV-COLA-330",
                Name = "330ml / Default",
                Price = 300m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantTiramisuSingle,
                ProductId = SeedData.ProductTiramisu,
                SizeId = SeedData.SizeSingle,
                ColourId = SeedData.ColourDefault,
                Sku = "DES-TIRA-S",
                Name = "Single / Default",
                Price = 900m,
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
                ProductVariantId = SeedData.VariantMargheritaSmall,
                QuantityOnHand = 50,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000042"),
                ProductVariantId = SeedData.VariantMargheritaLarge,
                QuantityOnHand = 30,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000043"),
                ProductVariantId = SeedData.VariantPepperoniMedium,
                QuantityOnHand = 40,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000044"),
                ProductVariantId = SeedData.VariantPepperoniLarge,
                QuantityOnHand = 25,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000045"),
                ProductVariantId = SeedData.VariantCarbonaraRegular,
                QuantityOnHand = 35,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000046"),
                ProductVariantId = SeedData.VariantCola330,
                QuantityOnHand = 200,
                ReservedQuantity = 0,
                ReorderLevel = 50
            },
            new InventoryStock
            {
                Id = new Guid("00000000-0000-0000-0000-000000000047"),
                ProductVariantId = SeedData.VariantTiramisuSingle,
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
        builder.ToTable("StockTransactions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasIndex(e => e.ProductVariantId);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.ProductVariant)
            .WithMany(e => e.StockTransactions)
            .HasForeignKey(e => e.ProductVariantId)
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
                Id = SeedData.SupplierFreshFoods,
                Name = "Fresh Foods Ltd",
                ContactName = "Nimal Perera",
                Email = "orders@freshfoods.lk",
                Phone = "+94 11 234 5678",
                IsActive = true
            },
            new Supplier
            {
                Id = SeedData.SupplierBeverageCo,
                Name = "Beverage Co",
                ContactName = "Kamal Silva",
                Email = "sales@beverageco.lk",
                Phone = "+94 11 876 5432",
                IsActive = true
            });
    }
}
