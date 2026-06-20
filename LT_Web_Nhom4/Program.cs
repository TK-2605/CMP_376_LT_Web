using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Repositories.Implementations;
using LT_Web_Nhom4.Repositories.Interfaces;
using LT_Web_Nhom4.Services.Implementations;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Login/Account/Login";
    options.LogoutPath = "/Identity/Login/Account/Logout";
    options.AccessDeniedPath = "/Identity/Login/Account/AccessDenied";
});

var jwtKey = builder.Configuration["Jwt:Key"];
var authenticationBuilder = builder.Services.AddAuthentication();
authenticationBuilder.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
    options.CallbackPath = "/signin-google";
});

if (!string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Length >= 32)
{
    authenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
}

builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IClassMemberRepository, ClassMemberRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<IQuestionOptionRepository, QuestionOptionRepository>();
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IExamQuestionRepository, ExamQuestionRepository>();
builder.Services.AddScoped<IExamAttemptRepository, ExamAttemptRepository>();
builder.Services.AddScoped<IAttemptAnswerRepository, AttemptAnswerRepository>();
builder.Services.AddScoped<IAntiCheatEventRepository, AntiCheatEventRepository>();
builder.Services.AddScoped<IUniqueCodeGenerator, UniqueCodeGenerator>();
builder.Services.AddScoped<IAccessPolicy, AccessPolicy>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailService, SmtpEmailSender>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPendingRegistrationService, PendingRegistrationService>();
builder.Services.AddSingleton<IPrivateMediaStorage, PrivateMediaStorage>();

builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.AreaViewLocationFormats.Insert(0, "/Areas/{2}/Login/Views/{1}/{0}.cshtml");
        options.AreaViewLocationFormats.Insert(1, "/Areas/{2}/Login/Views/Shared/{0}.cshtml");
    });

var app = builder.Build();
await SeedDefaultDataAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static async Task SeedDefaultDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AppSeed");

    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var roles = new[] { "Admin", "Student" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await context.Subjects.AnyAsync())
        {
            context.Subjects.Add(new Subject
            {
                Code = "GENERAL",
                Name = "Môn học chung",
                Description = "Môn học mặc định dùng khi cơ sở dữ liệu chưa có danh mục."
            });

            await context.SaveChangesAsync();
        }
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Default application data was not seeded.");
    }
}
