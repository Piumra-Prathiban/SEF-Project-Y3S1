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
builder.Services.AddScoped<IInventoryAnalysisModelClient, LocalInventoryAnalysisModelClient>();
builder.Services.AddScoped<IInventoryAnalysisAgentService, InventoryAnalysisAgentService>();
builder.Services.AddScoped<IInventoryAgentWorkflowService, InventoryAgentWorkflowService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IStorefrontService, StorefrontService>();
builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
builder.Services.AddScoped<IProductAvailabilityService, ProductAvailabilityService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ICustomerPreferenceTool, CustomerPreferenceTool>();
builder.Services.AddScoped<IProductSearchTool, ProductSearchTool>();
builder.Services.AddScoped<IWishlistTool, WishlistTool>();
builder.Services.AddScoped<IProductAvailabilityTool, ProductAvailabilityTool>();
builder.Services.AddScoped<IPersonalStylistRecommendationModel, GroundedPersonalStylistModel>();
builder.Services.AddScoped<IPersonalStylistOutputValidator, PersonalStylistOutputValidator>();
builder.Services.AddScoped<IAgentWorkflowRecorder, AgentWorkflowRecorder>();
builder.Services.AddScoped<IPersonalStylistAgent, PersonalStylistAgent>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
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

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
