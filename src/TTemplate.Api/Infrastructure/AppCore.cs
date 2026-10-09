using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using TTemplate.Api.Settings;

namespace TTemplate.Api.Infrastructure;

/// <summary>Шляхи застосунку. DataDir — з конфігу "DataDir" (відносний — від теки застосунку); у dev — data/ у корені репо.</summary>
public sealed class AppPaths(string dataDir) {
    public string DataDir { get; } = dataDir;
    public string DbPath => Path.Combine(DataDir, "app.db");
}

public static class AppCore {
    public static IServiceCollection AddAppCore(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env) {
        var dataDir = Path.GetFullPath(config["DataDir"] ?? "data", env.ContentRootPath);
        Directory.CreateDirectory(dataDir);
        services.AddSingleton(new AppPaths(dataDir));
        services.AddSingleton<Db>();
        services.AddSingleton<SettingsRegistry>();
        services.AddSingleton<SettingsStore>();
        services.AddDbMigration("CREATE TABLE IF NOT EXISTS settings(name TEXT PRIMARY KEY, value TEXT NOT NULL)");
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));
        services.ConfigureHttpJsonOptions(o => {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        return services;
    }

    /// <summary>Після Build: таблиці (міграції фіч реєструються через Db.AddMigration у їхніх Add*).</summary>
    public static void InitAppCore(this IServiceProvider services) {
        services.GetRequiredService<Db>().Migrate();
        foreach (var init in services.GetServices<IStartupInit>()) init.Init();
    }

    public static RouteGroupBuilder MapInfo(this RouteGroupBuilder api) {
        api.MapGet("/info", (SettingsStore settings) => {
            var s = settings.Get<AppSettings>();
            var version = typeof(AppCore).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            return Results.Ok(new { name = s.AppTitle, userName = s.UserName, version = version.Split('+')[0] });
        });
        return api;
    }
}

/// <summary>Одноразова ініціалізація фічі після міграцій (напр. позначити незавершені операції).</summary>
public interface IStartupInit {
    void Init();
}
