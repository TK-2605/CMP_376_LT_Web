using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var options = ParseArgs(args);
var targetConnectionString = Require(options, "target");
var resetTarget = options.ContainsKey("reset-target");
var verifyOnly = options.ContainsKey("verify-only");

var targetOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(targetConnectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure())
    .Options;

await using var target = new ApplicationDbContext(targetOptions);

if (verifyOnly)
{
    Console.WriteLine("Verifying PostgreSQL target...");
    if (!await target.Database.CanConnectAsync())
    {
        throw new InvalidOperationException("Cannot connect to the PostgreSQL target database.");
    }

    await PrintVerificationCountsAsync(target);
    return;
}

var sourceConnectionString = Require(options, "source");
var sourceOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(sourceConnectionString)
    .Options;

await using var source = new ApplicationDbContext(sourceOptions);

Console.WriteLine("Checking source database...");
if (!await source.Database.CanConnectAsync())
{
    throw new InvalidOperationException("Cannot connect to the SQL Server source database.");
}

Console.WriteLine("Preparing PostgreSQL target...");
if (resetTarget)
{
    await target.Database.ExecuteSqlRawAsync("""
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        GRANT ALL ON SCHEMA public TO public;
        """);
}

await target.Database.EnsureCreatedAsync();

await CopyIdentityAsync(source, target);
await CopyQuizHubDataAsync(source, target);
await ResetPostgresSequencesAsync(target);
await PrintVerificationCountsAsync(target);

Console.WriteLine("PostgreSQL sample migration completed.");

static async Task PrintVerificationCountsAsync(ApplicationDbContext target)
{
    Console.WriteLine("Verification counts:");
    Console.WriteLine($"  users: {await target.Users.CountAsync()}");
    Console.WriteLine($"  roles: {await target.Roles.CountAsync()}");
    Console.WriteLine($"  classes: {await target.Classes.CountAsync()}");
    Console.WriteLine($"  exams: {await target.Exams.CountAsync()}");
    Console.WriteLine($"  questions: {await target.Questions.CountAsync()}");
    Console.WriteLine($"  attempts: {await target.ExamAttempts.CountAsync()}");
    Console.WriteLine($"  antiCheatEvents: {await target.AntiCheatEvents.CountAsync()}");
}

static async Task CopyIdentityAsync(ApplicationDbContext source, ApplicationDbContext target)
{
    Console.WriteLine("Copying Identity tables...");

    target.Roles.AddRange(await source.Roles.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.Users.AddRange(await source.Users.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.RoleClaims.AddRange(await source.RoleClaims.AsNoTracking().ToListAsync());
    target.UserClaims.AddRange(await source.UserClaims.AsNoTracking().ToListAsync());
    target.UserLogins.AddRange(await source.UserLogins.AsNoTracking().ToListAsync());
    target.UserRoles.AddRange(await source.UserRoles.AsNoTracking().ToListAsync());
    target.UserTokens.AddRange(await source.UserTokens.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();
}

static async Task CopyQuizHubDataAsync(ApplicationDbContext source, ApplicationDbContext target)
{
    Console.WriteLine("Copying QuizHub tables...");

    target.Subjects.AddRange(await source.Subjects.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.Classes.AddRange(await source.Classes.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.ClassMembers.AddRange(await source.ClassMembers.AsNoTracking().ToListAsync());
    target.ClassMedia.AddRange(await source.ClassMedia.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.Questions.AddRange(await source.Questions.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.QuestionMedia.AddRange(await source.QuestionMedia.AsNoTracking().ToListAsync());
    target.QuestionOptions.AddRange(await source.QuestionOptions.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.Exams.AddRange(await source.Exams.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.ExamQuestions.AddRange(await source.ExamQuestions.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.ExamAttempts.AddRange(await source.ExamAttempts.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.AttemptAnswers.AddRange(await source.AttemptAnswers.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.AttemptAnswerSelections.AddRange(await source.AttemptAnswerSelections.AsNoTracking().ToListAsync());
    target.AntiCheatEvents.AddRange(await source.AntiCheatEvents.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();

    target.EmailOtps.AddRange(await source.EmailOtps.AsNoTracking().ToListAsync());
    target.RefreshTokens.AddRange(await source.RefreshTokens.AsNoTracking().ToListAsync());
    target.PendingRegistrations.AddRange(await source.PendingRegistrations.AsNoTracking().ToListAsync());
    await target.SaveChangesAsync();
}

static async Task ResetPostgresSequencesAsync(ApplicationDbContext target)
{
    Console.WriteLine("Resetting PostgreSQL identity sequences...");

    foreach (var tableName in new[]
    {
        "AspNetRoleClaims",
        "EmailOtps",
        "RefreshTokens",
        "PendingRegistrations",
        "Subjects",
        "Classes",
        "ClassMedia",
        "Questions",
        "QuestionMedia",
        "QuestionOptions",
        "Exams",
        "ExamQuestions",
        "ExamAttempts",
        "AttemptAnswers",
        "AntiCheatEvents"
    })
    {
        await ResetSequenceAsync(target, tableName);
    }
}

static async Task ResetSequenceAsync(ApplicationDbContext target, string tableName)
{
    var quotedTable = "\"" + tableName.Replace("\"", "\"\"") + "\"";
    var sql = $"""
        SELECT setval(
            pg_get_serial_sequence('{quotedTable}', 'Id'),
            COALESCE((SELECT MAX("Id") FROM {quotedTable}), 1),
            (SELECT COUNT(*) > 0 FROM {quotedTable})
        );
        """;

    await target.Database.ExecuteSqlRawAsync(sql);
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = arg[2..];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            values[key] = args[++i];
        }
        else
        {
            values[key] = "true";
        }
    }

    return values;
}

static string Require(IReadOnlyDictionary<string, string> values, string key)
{
    return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required argument: --{key}");
}
