using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Hubs;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Repositories.Implementations;
using LT_Web_Nhom4.Repositories.Interfaces;
using LT_Web_Nhom4.Services.Implementations;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:Key"] = "QuizHub_Localhost_Development_Jwt_Key_Only_For_Swagger_Testing_2026"
    });
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var usePostgreSql = IsPostgreSqlProvider(databaseProvider);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (usePostgreSql)
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
    if (builder.Environment.IsProduction())
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    }
});

var authenticationBuilder = builder.Services.AddAuthentication();
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleOAuthConfigured = !string.IsNullOrWhiteSpace(googleClientId)
    && !string.IsNullOrWhiteSpace(googleClientSecret);
if (googleOAuthConfigured)
{
    authenticationBuilder.AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;
            options.CallbackPath = "/signin-google";
        });
}

var configuredJwtKey = builder.Configuration["Jwt:Key"];
var jwtValidationKey = !string.IsNullOrWhiteSpace(configuredJwtKey) && configuredJwtKey.Length >= 32
    ? Encoding.UTF8.GetBytes(configuredJwtKey)
    : RandomNumberGenerator.GetBytes(64);

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
        IssuerSigningKey = new SymmetricSecurityKey(jwtValidationKey),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
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
builder.Services.AddScoped<IUniqueCodeGenerator, UniqueCodeGenerator>();
builder.Services.AddScoped<IAccessPolicy, AccessPolicy>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailService, SmtpEmailSender>();
builder.Services.AddScoped<IPendingRegistrationService, PendingRegistrationService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddHttpClient<IMeilisearchService, MeilisearchService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddScoped<IPrivateMediaStorage, PrivateMediaStorage>();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 8 * 1024;
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization()
    .AddRazorOptions(options =>
    {
        options.AreaViewLocationFormats.Insert(0, "/Areas/{2}/Login/Views/{1}/{0}.cshtml");
        options.AreaViewLocationFormats.Insert(1, "/Areas/{2}/Login/Views/Shared/{0}.cshtml");
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QuizHub API",
        Version = "v1",
        Description = "API kiểm thử OAuth, SMTP, xác nhận email, quên mật khẩu và JWT cho hệ thống thi trắc nghiệm."
    });

    options.TagActionsBy(api =>
    {
        var path = "/" + (api.RelativePath ?? string.Empty).ToLowerInvariant();
        if (path.StartsWith("/api/auth/register", StringComparison.Ordinal))
        {
            return ["Auth - Register"];
        }

        if (path.StartsWith("/api/auth/forgot-password", StringComparison.Ordinal))
        {
            return ["Auth - Forgot Password"];
        }

        if (path.StartsWith("/api/auth", StringComparison.Ordinal))
        {
            return ["Auth - Login JWT"];
        }

        if (path.StartsWith("/api/admin-data", StringComparison.Ordinal))
        {
            return ["CRUD - LocalDB"];
        }

        if (path.StartsWith("/api/features", StringComparison.Ordinal))
        {
            return ["Feature API Checks"];
        }

        return [api.ActionDescriptor.RouteValues["controller"] ?? "API"];
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT access token theo dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

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

var supportedCultures = new[]
{
    new CultureInfo("vi"),
    new CultureInfo("en"),
    new CultureInfo("ja"),
    new CultureInfo("ko"),
    new CultureInfo("zh")
};
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

var app = builder.Build();
if (!app.Environment.IsEnvironment("Testing"))
{
    if (app.Configuration.GetValue<bool>("Database:EnsureCreatedOnStartup"))
    {
        await EnsureDatabaseCreatedAsync(app);
    }
    else if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        await AdoptExistingSchemaMigrationsAsync(app);
        await ApplyDatabaseMigrationsAsync(app);
    }

    await EnsureStoredMediaFilesTableAsync(app);
    await LogDatabaseStartupStateAsync(app);
    await SeedDefaultDataAsync(app);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseWhen(IsSwaggerRequest, branch =>
        {
            branch.Use(async (context, next) =>
            {
                if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Admin"))
                {
                    await next();
                    return;
                }

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                var loginUrl = $"/Identity/Login/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
                context.Response.Redirect(loginUrl);
            });
        });
    }

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "QuizHub API v1");
        options.DocumentTitle = "QuizHub API Test";
    });
}

app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Application = "QuizHub",
    Utc = DateTimeOffset.UtcNow
})).AllowAnonymous();

if (!googleOAuthConfigured)
{
    app.MapGet("/signin-google", () => Results.Redirect(
        "/Identity/Login/Account/Login?oauthUnavailable=true"))
        .AllowAnonymous();
}

app.MapGet("/health/ready", async (
    ApplicationDbContext context,
    IMeilisearchService meilisearch,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var databaseReady = false;
    var databaseMessage = "Database is not reachable.";
    IReadOnlyList<string> pendingMigrations = Array.Empty<string>();
    string? migrationMessage = null;
    try
    {
        databaseReady = await context.Database.CanConnectAsync(cancellationToken);
        databaseMessage = databaseReady ? "Database is reachable." : databaseMessage;
        if (databaseReady)
        {
            pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        }
    }
    catch (Exception exception)
    {
        databaseMessage = exception.Message;
        migrationMessage = exception.GetType().Name;
    }

    var searchHealth = await meilisearch.GetHealthAsync(cancellationToken);
    var ready = databaseReady;
    var payload = new
    {
        Status = ready ? "Ready" : "Degraded",
        Database = new
        {
            Ready = databaseReady,
            Provider = context.Database.ProviderName,
            ConfiguredProvider = configuration["Database:Provider"] ?? "SqlServer",
            Message = databaseMessage,
            PendingMigrations = pendingMigrations,
            MigrationMessage = migrationMessage
        },
        Meilisearch = new
        {
            searchHealth.Configured,
            searchHealth.Reachable,
            searchHealth.Message
        },
        Utc = DateTimeOffset.UtcNow
    };

    return ready
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

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

app.MapHub<QuizChatHub>("/hubs/quiz-chat");

app.Run();

static bool IsSwaggerRequest(HttpContext context)
{
    return context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}

static bool IsPostgreSqlProvider(string? provider)
{
    return string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase);
}

static async Task EnsureDatabaseCreatedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseEnsureCreated");

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Database schema creation failed.");
        throw;
    }
}

static async Task AdoptExistingSchemaMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrationAdoption");

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!context.Database.IsRelational())
        {
            return;
        }

        var migrations = context.Database.GetMigrations().ToArray();
        if (migrations.Length == 0)
        {
            return;
        }

        var provider = context.Database.ProviderName ?? string.Empty;
        var hasExistingCoreSchema = await TableExistsAsync(context, provider, "AspNetUsers")
            && await TableExistsAsync(context, provider, "Exams");
        if (!hasExistingCoreSchema)
        {
            return;
        }

        await EnsureMigrationsHistoryTableAsync(context, provider);
        var appliedCount = Convert.ToInt32(await ExecuteScalarAsync(context, MigrationHistoryCountSql(provider)));
        if (appliedCount > 0)
        {
            return;
        }

        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "9.0.0";
        foreach (var migration in migrations)
        {
            await InsertMigrationHistoryAsync(context, provider, migration, productVersion);
        }

        logger.LogWarning(
            "Adopted existing database schema by stamping {MigrationCount} EF migrations. This runs only when core tables exist and migration history is empty.",
            migrations.Length);
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Could not adopt existing migration history. Normal migration flow will continue.");
    }
}

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Database migration failed.");
        if (app.Configuration.GetValue<bool>("Database:ContinueOnMigrationFailure"))
        {
            logger.LogWarning("Continuing startup because Database:ContinueOnMigrationFailure is enabled.");
            return;
        }

        throw;
    }
}

static async Task EnsureStoredMediaFilesTableAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMediaSchema");

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var provider = context.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "StoredMediaFiles" (
                    "Id" uuid NOT NULL,
                    "Category" character varying(100) NOT NULL,
                    "OriginalFileName" character varying(255) NOT NULL,
                    "ContentType" character varying(100) NOT NULL,
                    "Length" bigint NOT NULL,
                    "Content" bytea NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    CONSTRAINT "PK_StoredMediaFiles" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_StoredMediaFiles_Category_CreatedAt"
                    ON "StoredMediaFiles" ("Category", "CreatedAt");
                """);
            return;
        }

        await context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[StoredMediaFiles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [StoredMediaFiles] (
                    [Id] uniqueidentifier NOT NULL,
                    [Category] nvarchar(100) NOT NULL,
                    [OriginalFileName] nvarchar(255) NOT NULL,
                    [ContentType] nvarchar(100) NOT NULL,
                    [Length] bigint NOT NULL,
                    [Content] varbinary(max) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_StoredMediaFiles] PRIMARY KEY ([Id])
                );
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoredMediaFiles_Category_CreatedAt' AND object_id = OBJECT_ID(N'[StoredMediaFiles]'))
            BEGIN
                CREATE INDEX [IX_StoredMediaFiles_Category_CreatedAt] ON [StoredMediaFiles] ([Category], [CreatedAt]);
            END;
            """);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Stored media schema check failed.");
        throw;
    }
}

static async Task LogDatabaseStartupStateAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var canConnect = await context.Database.CanConnectAsync();
        var pending = canConnect
            ? (await context.Database.GetPendingMigrationsAsync()).ToArray()
            : Array.Empty<string>();
        logger.LogInformation(
            "Database startup state: provider={Provider}, configuredProvider={ConfiguredProvider}, canConnect={CanConnect}, pendingMigrations={PendingCount}, mediaStorage={MediaStorageProvider}.",
            context.Database.ProviderName,
            app.Configuration["Database:Provider"] ?? "SqlServer",
            canConnect,
            pending.Length,
            app.Configuration["Media:StorageProvider"] ?? "FileSystem");
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Could not read database startup state.");
    }
}

static async Task<bool> TableExistsAsync(ApplicationDbContext context, string provider, string tableName)
{
    if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        var result = await ExecuteScalarAsync(
            context,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @tableName
            )
            """,
            ("tableName", tableName));
        return result is bool exists && exists;
    }

    var sqlServerResult = await ExecuteScalarAsync(
        context,
        "SELECT CASE WHEN OBJECT_ID(@tableName, N'U') IS NULL THEN 0 ELSE 1 END",
        ("tableName", tableName));
    return Convert.ToInt32(sqlServerResult) == 1;
}

static async Task EnsureMigrationsHistoryTableAsync(ApplicationDbContext context, string provider)
{
    if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        await ExecuteNonQueryAsync(
            context,
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            )
            """);
        return;
    }

    await ExecuteNonQueryAsync(
        context,
        """
        IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
        BEGIN
            CREATE TABLE [__EFMigrationsHistory] (
                [MigrationId] nvarchar(150) NOT NULL,
                [ProductVersion] nvarchar(32) NOT NULL,
                CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
            );
        END
        """);
}

static string MigrationHistoryCountSql(string provider)
{
    return provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
        ? """SELECT COUNT(*) FROM "__EFMigrationsHistory" """
        : "SELECT COUNT(*) FROM [__EFMigrationsHistory]";
}

static async Task InsertMigrationHistoryAsync(
    ApplicationDbContext context,
    string provider,
    string migrationId,
    string productVersion)
{
    if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        await ExecuteNonQueryAsync(
            context,
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES (@migrationId, @productVersion)
            ON CONFLICT ("MigrationId") DO NOTHING
            """,
            ("migrationId", migrationId),
            ("productVersion", productVersion));
        return;
    }

    await ExecuteNonQueryAsync(
        context,
        """
        IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = @migrationId)
        BEGIN
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (@migrationId, @productVersion);
        END
        """,
        ("migrationId", migrationId),
        ("productVersion", productVersion));
}

static async Task<object?> ExecuteScalarAsync(
    ApplicationDbContext context,
    string commandText,
    params (string Name, object? Value)[] parameters)
{
    await using var command = await CreateCommandAsync(context, commandText, parameters);
    return await command.ExecuteScalarAsync();
}

static async Task ExecuteNonQueryAsync(
    ApplicationDbContext context,
    string commandText,
    params (string Name, object? Value)[] parameters)
{
    await using var command = await CreateCommandAsync(context, commandText, parameters);
    await command.ExecuteNonQueryAsync();
}

static async Task<DbCommand> CreateCommandAsync(
    ApplicationDbContext context,
    string commandText,
    params (string Name, object? Value)[] parameters)
{
    var connection = context.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    var command = connection.CreateCommand();
    command.CommandText = commandText;
    foreach (var (name, value) in parameters)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    return command;
}

static async Task SeedDefaultDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AppSeed");

    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = new[] { "Admin", "Student" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var legacyTeacherRole = await roleManager.FindByNameAsync("Teacher");
        if (legacyTeacherRole is not null)
        {
            var usersInLegacyRole = await userManager.GetUsersInRoleAsync("Teacher");
            foreach (var user in usersInLegacyRole)
            {
                await userManager.RemoveFromRoleAsync(user, "Teacher");
            }

            await roleManager.DeleteAsync(legacyTeacherRole);
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

public partial class Program;
