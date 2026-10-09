using Microsoft.Data.Sqlite;

namespace TTemplate.Api.Infrastructure;

/// <summary>Ідемпотентний скрипт схеми (CREATE TABLE IF NOT EXISTS …). Ядро й фічі додають свої через AddDbMigration.</summary>
public sealed record DbMigration(string Sql);

/// <summary>SQLite у DataDir/app.db.</summary>
public sealed class Db(AppPaths paths, IEnumerable<DbMigration> migrations) {
    public SqliteConnection Open() {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DbPath, Pooling = true }.ToString());
        c.Open();
        return c;
    }

    public void Migrate() {
        using var c = Open();
        Exec(c, "PRAGMA journal_mode=WAL;");
        foreach (var m in migrations) Exec(c, m.Sql);
    }

    public static int Exec(SqliteConnection c, string sql, params (string Name, object? Value)[] args) {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }
}

public static class DbExtensions {
    public static IServiceCollection AddDbMigration(this IServiceCollection services, string sql) =>
        services.AddSingleton(new DbMigration(sql));
}
