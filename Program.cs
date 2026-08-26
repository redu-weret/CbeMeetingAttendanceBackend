using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CbeMeetingAttendance.Data;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Middleware;
using CbeMeetingAttendance.Models;
using CbeMeetingAttendance.Repositories;
using CbeMeetingAttendance.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;
using OfficeOpenXml;

// Register custom font resolver for PDFSharp
PdfSharp.Fonts.GlobalFontSettings.FontResolver = new WindowsFontResolver();
ExcelPackage.License.SetNonCommercialOrganization("CBE");

var builder = WebApplication.CreateBuilder(args);

// Register DbContext with Dedicated SQL Server + Identity tables
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity user configuration (IdentityCore = API-friendly, no cookie login scheme)
builder.Services.AddIdentityCore<User>(options =>
{
    // Password settings
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;

    // Unique email
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// JWT Authentication configuration (from JwtSettings in appsettings.json)
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtKey = jwtSection["Key"] ?? "cbe";
var jwtIssuer = jwtSection["Issuer"] ?? "CbeMeetingAttendance";
var jwtAudiences = jwtSection.GetSection("Audiences").Get<string[]>()
                   ?? new[] { "CBE-Attendance-Web-App" };

// Hash short keys so HMAC-SHA256 has a valid 256-bit key
var signingKeyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(jwtKey));

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
        ValidIssuer = jwtIssuer,
        ValidAudiences = jwtAudiences,
        IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
        // Allow a small clock skew to tolerate minor server/client clock differences
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddAuthorization();

// CORS — allow the React frontend dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register Repository Layer
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register Service Layer
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IUserService, UserService>();

// Application-level payload encryption (hybrid AES-GCM + RSA-OAEP).
// The RSA private key is generated on first startup and stays on the backend only.
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// Configure Controllers
builder.Services.AddControllers();

// Allow multipart/form-data uploads large enough for the 5 MB profile picture limit
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 6 * 1024 * 1024; // 6 MB (slightly above 5 MB limit)
});

// Wrap validation errors in ApiResponse
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = string.Join(" | ", context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));

        var response = ApiResponse<object>.FailureResult($"Validation failed: {errors}");
        return new BadRequestObjectResult(response);
    };
});

// Swagger / OpenAPI with working JWT Authorize button (NSwag â€” same approach as TodoApi)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "v1";
    config.Title = "CbeMeetingAttendance API";
    config.Version = "v1";

    config.AddSecurity("Bearer", new OpenApiSecurityScheme
    {
        // Use ApiKey so Swagger UI sends exactly what the user pastes into the Authorize dialog.
        // This makes it unambiguous: paste the full header value including the "Bearer " prefix.
        Type = OpenApiSecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = OpenApiSecurityApiKeyLocation.Header,
        Description = "Enter the full header value including the Bearer prefix, e.g. 'Bearer eyJ...'."
    });

    config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi(options =>
    {
        options.Path = "/swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUi(options =>
    {
        options.DocumentTitle = "CbeMeetingAttendance";
        options.Path = "/swagger";
        options.DocumentPath = "/swagger/{documentName}/swagger.json";
    });
}

// Only redirect to HTTPS in production (phone/hotspot access uses HTTP)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

// Decrypt marked encrypted request bodies before controllers bind them.
// Responses are never encrypted. Requests without the marker pass through unchanged.
app.UseMiddleware<EncryptedRequestBodyMiddleware>();

// Serve uploaded profile pictures from wwwroot/Profiles
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SeedAdminUserAsync(scope.ServiceProvider, builder.Configuration);
}

await app.RunAsync();

static async Task SeedAdminUserAsync(IServiceProvider services, IConfiguration configuration)
{
    try
    {
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var userCount = await dbContext.Users.CountAsync();
        if (userCount > 0)
        {
            Console.WriteLine("[SEED] Users already exist, skipping seed.");
            return;
        }

        var adminSection = configuration.GetSection("AdminUser");
        var adminUserName = adminSection["UserName"];
        var adminEmail = adminSection["Email"];
        var adminPassword = adminSection["Password"];

        if (string.IsNullOrWhiteSpace(adminUserName) ||
            string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            Console.WriteLine("[SEED] AdminUser config is missing or incomplete, skipping seed.");
            return;
        }

        Console.WriteLine("[SEED] Creating first admin account...");

        const string adminRoleName = "Admin";

        // Ensure Admin role exists
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = adminRoleName,
                NormalizedName = adminRoleName.ToUpperInvariant()
            });

            if (!roleResult.Succeeded)
            {
                var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                Console.WriteLine($"[SEED] Failed to create Admin role: {roleErrors}");
                return;
            }
            Console.WriteLine("[SEED] Admin role created successfully.");
        }

        // Create admin employee record
        var adminEmployee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeId = "ADMIN",
            FullName = "System Administrator",
            Division = "Administration",
            JobTitle = "Administrator",
            Department = "IT",
            PhoneNumber = "0000000000",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Employees.Add(adminEmployee);
        await dbContext.SaveChangesAsync();
        Console.WriteLine("[SEED] Admin employee created successfully.");

        // Create admin user
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            UserName = adminUserName.Trim(),
            Email = adminEmail.Trim(),
            EmployeeId = adminEmployee.Id,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createResult.Succeeded)
        {
            var userErrors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            Console.WriteLine($"[SEED] Failed to create admin user: {userErrors}");
            return;
        }
        Console.WriteLine($"[SEED] Admin user '{adminUserName}' created successfully.");

        // Assign Admin role
        var roleAssignResult = await userManager.AddToRoleAsync(adminUser, adminRoleName);
        if (!roleAssignResult.Succeeded)
        {
            var assignErrors = string.Join(", ", roleAssignResult.Errors.Select(e => e.Description));
            Console.WriteLine($"[SEED] Failed to assign Admin role: {assignErrors}");
            return;
        }
        Console.WriteLine("[SEED] Admin role assigned successfully.");
        Console.WriteLine("[SEED] ========================================");
        Console.WriteLine("[SEED] First admin account created successfully!");
        Console.WriteLine($"[SEED] Username: {adminUserName}");
        Console.WriteLine($"[SEED] Email: {adminEmail}");
        Console.WriteLine($"[SEED] Password: {adminPassword}");
        Console.WriteLine("[SEED] ========================================");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SEED] ERROR: {ex.Message}");
        Console.WriteLine($"[SEED] Stack: {ex.StackTrace}");
    }
}

