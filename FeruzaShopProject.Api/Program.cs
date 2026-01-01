using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Application.Mapper;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Infrastructre.Authorization;
using FeruzaShopProject.Infrastructre.Data;
using FeruzaShopProject.Infrastructre.Services;
using FeruzaShopProject.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShopMgtSys.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<ShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ShopDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured")))
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogError($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BranchAccess", policy => policy.AddRequirements(new BranchAccessRequirement()));
    options.AddPolicy("RequireSalesRole", policy => policy.RequireRole(Role.Sales.ToString()));
    options.AddPolicy("RequireManagerOrFinanceRole", policy =>
        policy.RequireRole(Role.Manager.ToString(), Role.Finance.ToString()));
});

builder.Services.AddSingleton<IAuthorizationHandler, BranchAccessHandler>();

// AutoMapper configurations
builder.Services.AddAutoMapper(typeof(BranchMapper));
builder.Services.AddAutoMapper(typeof(CategoryMapper));
builder.Services.AddAutoMapper(typeof(ProductMapper));
builder.Services.AddAutoMapper(typeof(BankAccountMapper));
builder.Services.AddAutoMapper(typeof(TransactionMapper));
builder.Services.AddAutoMapper(typeof(DailySalesMapper));
builder.Services.AddAutoMapper(typeof(CustomerMapper));
builder.Services.AddAutoMapper(typeof(PainterMapper));
builder.Services.AddAutoMapper(typeof(SupplierMapper));
builder.Services.AddAutoMapper(typeof(PurchaseMapper));
builder.Services.AddAutoMapper(typeof(ProductExchangeMapper));

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductExchangeService, ProductExchangeService>();
builder.Services.AddScoped<IStockService, StockService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT Bearer Authorization
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Feruza Shop API",
        Version = "v1",
        Description = "API for Feruza Shop Management System",
        Contact = new OpenApiContact
        {
            Name = "Your Name",
            Email = "your.email@example.com"
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. 
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });

    // Optional: Include XML comments if you have them
    // var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    // c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedRolesAsync(roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding roles");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Feruza Shop API v1");

        // Optional: Configure Swagger UI appearance
        c.RoutePrefix = "swagger"; // Makes swagger UI available at /swagger
        c.DisplayRequestDuration();
        c.EnablePersistAuthorization(); // Remember authorization between page reloads
        c.EnableDeepLinking(); // Enables deep linking for tags and operations
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();