using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SEF_Project.Api.Configuration;
using SEF_Project.Api.Data;
using System.Text;
using SEF_Project.Api.Services.Auth;
using SEF_Project.Api.Services.Orders;
using SEF_Project.Api.Services.Shopping;
using SEF_Project.Api.Services.Profile;
using SEF_Project.Api.Services.Recommendations;
using SEF_Project.Api.Middleware;
using Microsoft.OpenApi.Models;
using System.Reflection;


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
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// Add services to the container.
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrderService, OrderService>();
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


builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SEF Project API",
        Version = "v1",
        Description = "Shopping discovery, customer experience, and controlled product recommendation APIs."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();
app.UseExceptionHandler();


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
