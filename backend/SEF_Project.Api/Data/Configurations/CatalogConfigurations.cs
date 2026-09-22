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
            new Category
            {
                Id = SeedData.CategoryPizza,
                Name = "Pizza",
                IsActive = true
            },
            new Category
            {
                Id = SeedData.CategoryPasta,
                Name = "Pasta",
                IsActive = true
            },
            new Category
            {
                Id = SeedData.CategoryBeverages,
                Name = "Beverages",
                IsActive = true
            },
            new Category
            {
                Id = SeedData.CategoryDesserts,
                Name = "Desserts",
                IsActive = true
            });
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
                SupplierId = SeedData.SupplierFreshFoods,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductPepperoni,
                Name = "Pepperoni Pizza",
                Description = "Pepperoni with mozzarella.",
                SupplierId = SeedData.SupplierFreshFoods,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductCarbonara,
                Name = "Spaghetti Carbonara",
                Description = "Creamy pasta with pancetta.",
                SupplierId = SeedData.SupplierFreshFoods,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductCola,
                Name = "Cola",
                Description = "Carbonated soft drink.",
                SupplierId = SeedData.SupplierBeverageCo,
                IsActive = true
            },
            new Product
            {
                Id = SeedData.ProductTiramisu,
                Name = "Tiramisu",
                Description = "Classic coffee dessert.",
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
        builder.Property(e => e.Size).HasMaxLength(50);
        builder.Property(e => e.Colour).HasMaxLength(50);
        builder.Property(e => e.Price).HasPrecision(18, 2);

        builder.HasIndex(e => e.Sku).IsUnique();

        builder.HasOne(e => e.Product)
            .WithMany(e => e.Variants)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new ProductVariant
            {
                Id = SeedData.VariantMargheritaSmall,
                ProductId = SeedData.ProductMargherita,
                Sku = "PIZ-MARG-S",
                Name = "Small",
                Size = "Small",
                Price = 1200m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantMargheritaLarge,
                ProductId = SeedData.ProductMargherita,
                Sku = "PIZ-MARG-L",
                Name = "Large",
                Size = "Large",
                Price = 2200m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantPepperoniMedium,
                ProductId = SeedData.ProductPepperoni,
                Sku = "PIZ-PEP-M",
                Name = "Medium",
                Size = "Medium",
                Price = 1600m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantPepperoniLarge,
                ProductId = SeedData.ProductPepperoni,
                Sku = "PIZ-PEP-L",
                Name = "Large",
                Size = "Large",
                Price = 2600m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantCarbonaraRegular,
                ProductId = SeedData.ProductCarbonara,
                Sku = "PST-CARB-R",
                Name = "Regular",
                Size = "Regular",
                Price = 1800m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantCola330,
                ProductId = SeedData.ProductCola,
                Sku = "BEV-COLA-330",
                Name = "330ml",
                Size = "330ml",
                Price = 300m,
                IsActive = true
            },
            new ProductVariant
            {
                Id = SeedData.VariantTiramisuSingle,
                ProductId = SeedData.ProductTiramisu,
                Sku = "DES-TIRA-S",
                Name = "Single",
                Size = "Single",
                Price = 900m,
                IsActive = true
            });
    }
}

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");

        builder.HasKey(e => new { e.ProductId, e.CategoryId });

        builder.HasOne(e => e.Product)
            .WithMany(e => e.ProductCategories)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Category)
            .WithMany(e => e.ProductCategories)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new ProductCategory
            {
                ProductId = SeedData.ProductMargherita,
                CategoryId = SeedData.CategoryPizza
            },
            new ProductCategory
            {
                ProductId = SeedData.ProductPepperoni,
                CategoryId = SeedData.CategoryPizza
            },
            new ProductCategory
            {
                ProductId = SeedData.ProductCarbonara,
                CategoryId = SeedData.CategoryPasta
            },
            new ProductCategory
            {
                ProductId = SeedData.ProductCola,
                CategoryId = SeedData.CategoryBeverages
            },
            new ProductCategory
            {
                ProductId = SeedData.ProductTiramisu,
                CategoryId = SeedData.CategoryDesserts
            });
    }
}

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventory", table =>
        {
            table.HasCheckConstraint("CK_Inventory_QuantityOnHand", "\"QuantityOnHand\" >= 0");
            table.HasCheckConstraint("CK_Inventory_ReservedQuantity", "\"ReservedQuantity\" >= 0");
            table.HasCheckConstraint("CK_Inventory_ReorderLevel", "\"ReorderLevel\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.ProductVariantId).IsUnique();

        builder.HasOne(e => e.ProductVariant)
            .WithOne(e => e.Inventory)
            .HasForeignKey<Inventory>(e => e.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000041"),
                ProductVariantId = SeedData.VariantMargheritaSmall,
                QuantityOnHand = 50,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000042"),
                ProductVariantId = SeedData.VariantMargheritaLarge,
                QuantityOnHand = 30,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000043"),
                ProductVariantId = SeedData.VariantPepperoniMedium,
                QuantityOnHand = 40,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000044"),
                ProductVariantId = SeedData.VariantPepperoniLarge,
                QuantityOnHand = 25,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000045"),
                ProductVariantId = SeedData.VariantCarbonaraRegular,
                QuantityOnHand = 35,
                ReservedQuantity = 0,
                ReorderLevel = 10
            },
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000046"),
                ProductVariantId = SeedData.VariantCola330,
                QuantityOnHand = 200,
                ReservedQuantity = 0,
                ReorderLevel = 50
            },
            new Inventory
            {
                Id = new Guid("00000000-0000-0000-0000-000000000047"),
                ProductVariantId = SeedData.VariantTiramisuSingle,
                QuantityOnHand = 20,
                ReservedQuantity = 0,
                ReorderLevel = 5
            });
    }
}

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasIndex(e => e.ProductVariantId);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.ProductVariant)
            .WithMany(e => e.InventoryTransactions)
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

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ImageUrl).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.AltText).HasMaxLength(200);

        builder.HasIndex(e => e.ProductId);

        builder.HasOne(e => e.Product)
            .WithMany(e => e.Images)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
