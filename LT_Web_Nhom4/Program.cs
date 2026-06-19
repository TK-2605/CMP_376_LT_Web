using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Repositories.Implementations;
using LT_Web_Nhom4.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor;

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
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Login/Account/Login";
    options.LogoutPath = "/Identity/Login/Account/Logout";
    options.AccessDeniedPath = "/Identity/Login/Account/AccessDenied";
});

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

builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.AreaViewLocationFormats.Insert(0, "/Areas/{2}/Login/Views/{1}/{0}.cshtml");
        options.AreaViewLocationFormats.Insert(1, "/Areas/{2}/Login/Views/Shared/{0}.cshtml");
    });

var app = builder.Build();
await SeedDefaultRolesAsync(app);

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

static async Task SeedDefaultRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var roles = new[] { "Admin", "Teacher", "Student" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Default identity roles were not seeded.");
    }
}
