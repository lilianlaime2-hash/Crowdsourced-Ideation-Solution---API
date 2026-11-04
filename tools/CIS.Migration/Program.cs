namespace CIS.Migration;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("seed" or "migrate" or "verify") ||
            (args[0] == "verify" ? args.Length != 3 || args[1] != "--report" || string.IsNullOrWhiteSpace(args[2]) : args.Length != 1))
        {
            Console.Error.WriteLine("Usage: seed | migrate | verify --report <path>"); return 2;
        }
        var mysql = Environment.GetEnvironmentVariable("CIS_MYSQL_CONNECTION");
        var mongo = Environment.GetEnvironmentVariable("CIS_MONGO_CONNECTION");
        if (string.IsNullOrWhiteSpace(mysql) || (args[0] != "seed" && string.IsNullOrWhiteSpace(mongo)))
        {
            Console.Error.WriteLine("Set CIS_MYSQL_CONNECTION and CIS_MONGO_CONNECTION (including database names)."); return 2;
        }
        try
        {
            var runner = new MigrationRunner(mysql, mongo ?? "");
            if (args[0] == "seed") { await runner.SeedAsync(); Console.WriteLine("Deterministic phase 2 source fixture created."); return 0; }
            if (args[0] == "migrate") { await runner.MigrateAsync(); Console.WriteLine("Source snapshot copied to empty destination."); return 0; }
            var report = await runner.VerifyAsync();
            var path = Path.GetFullPath(args[2]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(report.Passed ? "Verification passed." : "Verification failed; see report."); return report.Passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            // Never echo connection strings, passwords or document field values.
            Console.Error.WriteLine($"{args[0]} failed ({ex.GetType().Name}). Check schema, permissions, and database availability."); return 1;
        }
    }
}
