namespace SEF_Project.Api.Data.Configurations;

public static class SeedData
{
    public static readonly Guid CategoryPizza = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid CategoryPasta = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid CategoryBeverages = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid CategoryDesserts = new("00000000-0000-0000-0000-000000000004");

    public static readonly Guid SupplierFreshFoods = new("00000000-0000-0000-0000-000000000011");
    public static readonly Guid SupplierBeverageCo = new("00000000-0000-0000-0000-000000000012");

    public static readonly Guid ProductMargherita = new("00000000-0000-0000-0000-000000000021");
    public static readonly Guid ProductPepperoni = new("00000000-0000-0000-0000-000000000022");
    public static readonly Guid ProductCarbonara = new("00000000-0000-0000-0000-000000000023");
    public static readonly Guid ProductCola = new("00000000-0000-0000-0000-000000000024");
    public static readonly Guid ProductTiramisu = new("00000000-0000-0000-0000-000000000025");

    public static readonly Guid VariantMargheritaSmall = new("00000000-0000-0000-0000-000000000031");
    public static readonly Guid VariantMargheritaLarge = new("00000000-0000-0000-0000-000000000032");
    public static readonly Guid VariantPepperoniMedium = new("00000000-0000-0000-0000-000000000033");
    public static readonly Guid VariantPepperoniLarge = new("00000000-0000-0000-0000-000000000034");
    public static readonly Guid VariantCarbonaraRegular = new("00000000-0000-0000-0000-000000000035");
    public static readonly Guid VariantCola330 = new("00000000-0000-0000-0000-000000000036");
    public static readonly Guid VariantTiramisuSingle = new("00000000-0000-0000-0000-000000000037");

    public static readonly Guid CampaignSummer = new("00000000-0000-0000-0000-000000000051");
    public static readonly Guid PromotionPizza20 = new("00000000-0000-0000-0000-000000000052");
    public static readonly Guid CouponSummer20 = new("00000000-0000-0000-0000-000000000053");
    public static readonly Guid CampaignWeekendRefresh = new("00000000-0000-0000-0000-000000000054");
    public static readonly Guid PromotionColaFixed = new("00000000-0000-0000-0000-000000000055");
    public static readonly Guid PromotionDessert10 = new("00000000-0000-0000-0000-000000000056");
    public static readonly Guid PromotionFreeDelivery = new("00000000-0000-0000-0000-000000000057");
    public static readonly Guid CouponFreeDelivery = new("00000000-0000-0000-0000-000000000058");
}
