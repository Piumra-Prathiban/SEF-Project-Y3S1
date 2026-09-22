using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SEF_Project.Api.Data;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Marketing;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Models.Shopping;

namespace SEF_Project.Api.Tests;

public class DatabaseModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=sef_project_db")
            .Options;

        using var context = new AppDbContext(options);
        return context.Model;
    }

    [Fact]
    public void OrderItem_ShouldReference_ProductVariant()
    {
        var model = BuildModel();

        var orderItem = model.FindEntityType(typeof(OrderItem))!;
        var fk = orderItem.GetForeignKeys()
            .Single(f => f.Properties.Any(p => p.Name == nameof(OrderItem.ProductVariantId)));

        Assert.Equal(typeof(ProductVariant), fk.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void CartItem_ShouldReference_ProductVariant()
    {
        var model = BuildModel();

        var cartItem = model.FindEntityType(typeof(CartItem))!;
        var fk = cartItem.GetForeignKeys()
            .Single(f => f.Properties.Any(p => p.Name == nameof(CartItem.ProductVariantId)));

        Assert.Equal(typeof(ProductVariant), fk.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void ProductVariant_Sku_ShouldBeUnique()
    {
        var model = BuildModel();

        var variant = model.FindEntityType(typeof(ProductVariant))!;
        var skuIndex = variant.GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == nameof(ProductVariant.Sku)));

        Assert.True(skuIndex.IsUnique);
    }

    [Fact]
    public void Order_OrderNumber_ShouldBeUnique()
    {
        var model = BuildModel();

        var order = model.FindEntityType(typeof(Order))!;
        var index = order.GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == nameof(Order.OrderNumber)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Coupon_Code_ShouldBeUnique()
    {
        var model = BuildModel();

        var coupon = model.FindEntityType(typeof(Coupon))!;
        var index = coupon.GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == nameof(Coupon.Code)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void MonetaryProperties_ShouldUse_DecimalPrecision18_2()
    {
        var model = BuildModel();

        var unitPrice = model.FindEntityType(typeof(OrderItem))!
            .FindProperty(nameof(OrderItem.UnitPrice))!;
        var variantPrice = model.FindEntityType(typeof(ProductVariant))!
            .FindProperty(nameof(ProductVariant.Price))!;

        Assert.Equal(18, unitPrice.GetPrecision());
        Assert.Equal(2, unitPrice.GetScale());
        Assert.Equal(18, variantPrice.GetPrecision());
        Assert.Equal(2, variantPrice.GetScale());
    }

    [Fact]
    public void SharedIdentity_ShouldUse_IntegerPrimaryKeys()
    {
        var model = BuildModel();

        Assert.Equal(
            typeof(int),
            model.FindEntityType(typeof(User))!.FindPrimaryKey()!
                .Properties.Single().ClrType);
        Assert.Equal(
            typeof(int),
            model.FindEntityType(typeof(Role))!.FindPrimaryKey()!
                .Properties.Single().ClrType);
        Assert.Equal(
            typeof(int),
            model.FindEntityType(typeof(Customer))!.FindPrimaryKey()!
                .Properties.Single().ClrType);
    }

    [Fact]
    public void NewBusinessEntities_ShouldUse_GuidPrimaryKeys()
    {
        var model = BuildModel();

        var guidKeyedTypes = new[]
        {
            typeof(Product),
            typeof(ProductVariant),
            typeof(Order),
            typeof(Cart),
            typeof(AgentWorkflow)
        };

        foreach (var type in guidKeyedTypes)
        {
            var key = model.FindEntityType(type)!.FindPrimaryKey()!;
            Assert.Equal(typeof(Guid), key.Properties.Single().ClrType);
        }
    }

    [Fact]
    public void Product_ShouldHave_ManyVariants()
    {
        var model = BuildModel();

        var variant = model.FindEntityType(typeof(ProductVariant))!;
        var fk = variant.GetForeignKeys()
            .Single(f => f.Properties.Any(p => p.Name == nameof(ProductVariant.ProductId)));

        Assert.Equal(typeof(Product), fk.PrincipalEntityType.ClrType);
    }
}
