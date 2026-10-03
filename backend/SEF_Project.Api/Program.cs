using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SEF_Project.Api.Configuration;
using SEF_Project.Api.Data;
using System.Text;
using SEF_Project.Api.Services.Auth;
using SEF_Project.Api.Services.AgenticAI;
using SEF_Project.Api.Services.Catalog;
using SEF_Project.Api.Services.Orders;
using SEF_Project.Api.Services.Storefront;
using SEF_Project.Api.Services.Shopping;
using SEF_Project.Api.Services.Profile;
using SEF_Project.Api.Services.Recommendations;
using SEF_Project.Api.Services.Marketing;
using SEF_Project.Api.Services.Analytics;
using SEF_Project.Api.AI.InventoryPromotion;
using SEF_Project.Api.AI.DeepSeek;
using SEF_Project.Api.Middleware;
using System.Reflection;


// Local secrets live in the gitignored backend/SEF_Project.Api/.env (see
// .env.example). Loaded before the builder so they become configuration —
// double underscores map to nested keys (SeedAdmin__Email -> SeedAdmin:Email).
DotEnvLoader.Load(
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<PersonalStylistAgentOptions>(
    builder.Configuration.GetSection("PersonalStylistAgent"));
builder.Services.Configure<InventoryAnalysisAgentOptions>(
    builder.Configuration.GetSection(InventoryAnalysisAgentOptions.SectionName));
builder.Services.Configure<DeepSeekOptions>(
    builder.Configuration.GetSection(DeepSeekOptions.SectionName));

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");
var allowedOrigins = CorsOriginPolicy.Normalize(
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>());

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Key))
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Authentication is required to access this resource.",
                    Instance = context.Request.Path
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "You do not have permission to access this resource.",
                    Instance = context.Request.Path
                });
            }
        };
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// Add services to the container.
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IInventoryAgentToolRegistry, InventoryAgentToolRegistry>();
builder.Services.AddScoped<LocalInventoryAnalysisModelClient>();
builder.Services.AddHttpClient<IDeepSeekChatCompletionsClient, DeepSeekChatCompletionsClient>();
builder.Services.AddScoped<DeepSeekInventoryAnalysisModelClient>();
builder.Services.AddScoped<IInventoryAnalysisModelClient>(services =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryAnalysisAgentOptions>>()
        .Value;

    return string.Equals(options.Provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)
        ? services.GetRequiredService<DeepSeekInventoryAnalysisModelClient>()
        : services.GetRequiredService<LocalInventoryAnalysisModelClient>();
});
builder.Services.AddScoped<IInventoryAnalysisAgentService, InventoryAnalysisAgentService>();
builder.Services.AddScoped<IInventoryAgentWorkflowService, InventoryAgentWorkflowService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IStorefrontService, StorefrontService>();
builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
builder.Services.AddScoped<IProductAvailabilityService, ProductAvailabilityService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICustomerPreferenceTool, CustomerPreferenceTool>();
builder.Services.AddScoped<IProductSearchTool, ProductSearchTool>();
builder.Services.AddScoped<IWishlistTool, WishlistTool>();
builder.Services.AddScoped<IProductAvailabilityTool, ProductAvailabilityTool>();
builder.Services.AddScoped<GroundedPersonalStylistModel>();
builder.Services.AddScoped<DeepSeekPersonalStylistModel>();
builder.Services.AddScoped<IPersonalStylistRecommendationModel>(services =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<PersonalStylistAgentOptions>>()
        .Value;

    return string.Equals(options.Provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)
        ? services.GetRequiredService<DeepSeekPersonalStylistModel>()
        : services.GetRequiredService<GroundedPersonalStylistModel>();
});
builder.Services.AddScoped<IPersonalStylistOutputValidator, PersonalStylistOutputValidator>();
builder.Services.AddScoped<IAgentWorkflowRecorder, AgentWorkflowRecorder>();
builder.Services.AddScoped<IPersonalStylistAgent, PersonalStylistAgent>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IPromotionPricingService, PromotionPricingService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IPromotionOfferService, PromotionOfferService>();

// Inventory & Promotion Agent: allow-listed read-only tools, replaceable
// proposal model (deterministic local policy by default), orchestrator.
builder.Services.Configure<InventoryPromotionAgentOptions>(
    builder.Configuration.GetSection(InventoryPromotionAgentOptions.SectionName));
builder.Services.AddScoped<IPromotionAgentTool, GetSalesVelocityTool>();
builder.Services.AddScoped<IPromotionAgentTool, GetInventoryTool>();
builder.Services.AddScoped<IPromotionAgentTool, GetActivePromotionsTool>();
builder.Services.AddScoped<IPromotionAgentTool, GetProductDetailsTool>();
builder.Services.AddScoped<IPromotionAgentTool, GetProductPricingTool>();
builder.Services.AddScoped<IPromotionAgentTool, CalculatePromotionTool>();
builder.Services.AddScoped<PromotionAgentToolRegistry>();
builder.Services.AddScoped<LocalPromotionProposalModel>();
builder.Services.AddHttpClient<IDeepSeekChatCompletionsClient, DeepSeekChatCompletionsClient>();
builder.Services.AddScoped<DeepSeekPromotionProposalModel>();
builder.Services.AddScoped<IPromotionProposalModel>(services =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryPromotionAgentOptions>>()
        .Value;

    return string.Equals(options.Provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)
        ? services.GetRequiredService<DeepSeekPromotionProposalModel>()
        : services.GetRequiredService<LocalPromotionProposalModel>();
});
builder.Services.AddScoped<IInventoryPromotionAgent, InventoryPromotionAgentService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();

        // Flutter web picks a new random port on every `flutter run`, so in
        // local development allow any localhost origin rather than only the
        // pinned origins configured above.
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                    uri.Scheme == Uri.UriSchemeHttps) &&
                (uri.Host == "localhost" || uri.Host == "127.0.0.1"));
        }
    });
});


builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred.",
                Instance = context.HttpContext.Request.Path
            };

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Clothic API",
        Version = "v1",
        Description = "REST API for the Clothic AI-powered fashion commerce and retail platform: authentication, catalog, inventory, orders, storefront, shopping and recommendations."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter the JWT access token returned by the shared authentication API.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.OperationFilter<JwtSecurityOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
app.UseExceptionHandler();

var demoCommand = args
    .Select(argument => argument.Trim().ToLowerInvariant())
    .FirstOrDefault(argument => argument is "seed-demo" or "reset-demo" or "reseed-demo");

if (demoCommand is not null)
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine(
            "Demo data commands are available only in the Development environment.");
        Environment.ExitCode = 1;
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();

    try
    {
        await context.Database.MigrateAsync();
        var seeder = new DemoDataSeeder(
            context,
            services.GetRequiredService<IPasswordService>(),
            services.GetRequiredService<TimeProvider>());
        var options = builder.Configuration
            .GetSection(DemoDataOptions.SectionName)
            .Get<DemoDataOptions>() ?? new DemoDataOptions();

        DemoDataResult result;
        if (demoCommand == "reset-demo")
        {
            result = await seeder.ResetAsync();
        }
        else if (demoCommand == "reseed-demo")
        {
            await seeder.ResetAsync();
            result = await seeder.SeedAsync(options);
        }
        else
        {
            result = await seeder.SeedAsync(options);
        }

        Console.WriteLine(result.Message);
        if (demoCommand is "seed-demo" or "reseed-demo")
        {
            Console.WriteLine(
                $"Accounts: {DemoDataSeeder.CustomerEmail}, "
                + $"{DemoDataSeeder.StaffEmail}, {DemoDataSeeder.AdministratorEmail}");
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Demo data command failed: {exception.Message}");
        Environment.ExitCode = 1;
    }

    return;
}

// Bootstrap the Administrator account from configuration when it is missing:
// registration only ever creates Customers, so this is the only built-in way
// to get a staff/admin login on a fresh database.
using (var scope = app.Services.CreateScope())
{
    await AdminUserSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<IPasswordService>(),
        builder.Configuration.GetSection("SeedAdmin").Get<AdminSeedSettings>()
            ?? new AdminSeedSettings(),
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Serves the authored product imagery in wwwroot/images/products.
app.UseStaticFiles();

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes Program to WebApplicationFactory in the integration tests.
public partial class Program { }
