using System;
using Npgsql;
var cs = args[0];
await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
await using (var cmd = new NpgsqlCommand(@"
SELECT column_name FROM information_schema.columns
WHERE table_name = 'daily_patient_feedbacks'
ORDER BY ordinal_position;
", conn))
{
    await using var r = await cmd.ExecuteReaderAsync();
    Console.WriteLine("COLUMNS:");
    while (await r.ReadAsync()) Console.WriteLine("- " + r.GetString(0));
}
await using (var cmd = new NpgsqlCommand(@"SELECT ""MigrationId"" FROM ""__EFMigrationsHistory"" ORDER BY 1;", conn))
{
    await using var r = await cmd.ExecuteReaderAsync();
    Console.WriteLine("MIGRATIONS:");
    while (await r.ReadAsync()) Console.WriteLine("- " + r.GetString(0));
}
