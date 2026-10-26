using RoleBasedApp.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
//using RoleBasedApp.Data;
using RoleBasedApp.Models;
using RoleBasedApp.Security;
using RoleBasedApp.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token here."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString(
                "DefaultConnection")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Configure Jwt:Key using User Secrets.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Jwt:Issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Jwt:Audience is missing.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "Permission:USER_VIEW",
        policy => policy.Requirements.Add(
            new PermissionRequirement("USER_VIEW")));

    options.AddPolicy(
        "Permission:USER_CREATE",
        policy => policy.Requirements.Add(
            new PermissionRequirement("USER_CREATE")));

    options.AddPolicy(
        "Permission:USER_EDIT",
        policy => policy.Requirements.Add(
            new PermissionRequirement("USER_EDIT")));

    options.AddPolicy(
        "Permission:USER_APPROVE",
        policy => policy.Requirements.Add(
            new PermissionRequirement("USER_APPROVE")));

    options.AddPolicy(
        "Permission:USER_REJECT",
        policy => policy.Requirements.Add(
            new PermissionRequirement("USER_REJECT")));

    options.AddPolicy(
        "Permission:ROLE_VIEW",
        policy => policy.Requirements.Add(
            new PermissionRequirement("ROLE_VIEW")));

    options.AddPolicy(
        "Permission:ROLE_CREATE",
        policy => policy.Requirements.Add(
            new PermissionRequirement("ROLE_CREATE")));

    options.AddPolicy(
        "Permission:ROLE_EDIT",
        policy => policy.Requirements.Add(
            new PermissionRequirement("ROLE_EDIT")));

    options.AddPolicy(
        "Permission:ROLE_ASSIGN",
        policy => policy.Requirements.Add(
            new PermissionRequirement("ROLE_ASSIGN")));

    options.AddPolicy(
        "Permission:AUDIT_VIEW",
        policy => policy.Requirements.Add(
            new PermissionRequirement("AUDIT_VIEW")));
});

builder.Services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuditService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://localhost:51147",
                "http://localhost:51571",
                "http://localhost:63982",
                "http://localhost:64313"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

//app.UseCors("AngularClient");

//app.UseMiddleware<RoleBasedApp.Middleware.RequestLoggingMiddleware>();

//app.UseAuthorization();

//app.MapControllers();

app.UseHttpsRedirection();

//app.UseRouting();

app.UseCors("AngularClient");

app.UseAuthentication();

app.UseMiddleware<RoleBasedApp.Middleware.RequestLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();

await DatabaseInitializer.InitializeAsync(
    app.Services,
    app.Configuration);

app.Run();
