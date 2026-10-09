using System.Reflection;
using System.Text.Json;
using TTemplate.Api.Infrastructure;

namespace TTemplate.Api.Settings;

public static class SettingsEndpoints {
    /// <summary>Зареєструвати клас налаштувань як розділ (атрибут [SettingsSection] обовʼязковий).</summary>
    public static IServiceCollection AddSettingsSection<T>(this IServiceCollection services) where T : class, new() {
        var a = typeof(T).GetCustomAttribute<SettingsSectionAttribute>()
            ?? throw new InvalidOperationException($"{typeof(T).Name}: потрібен атрибут [SettingsSection]");
        services.AddSingleton(new SettingsSectionInfo(typeof(T), a.Key, a.Caption, a.Icon, a.Order));
        // Зручно інжектити сам розділ: актуальні значення на момент запиту.
        services.AddTransient(sp => sp.GetRequiredService<SettingsStore>().Get<T>());
        return services;
    }

    public static RouteGroupBuilder MapSettings(this RouteGroupBuilder api) {
        api.MapGet("/settings", (SettingsRegistry registry, SettingsStore store) => Results.Ok(new {
            sections = registry.Sections.Select(s => new { key = s.Key, caption = s.Caption, icon = s.Icon }),
            items = store.Describe(),
        }));

        api.MapPut("/settings/{name}", (string name, SetSettingRequest body, SettingsRegistry registry, SettingsStore store) => {
            var d = registry.Find(name);
            if (d == null) return ApiErrors.NotFound($"Налаштування {name} не існує");
            var error = store.Set(d, body.Value);
            return error == null ? Results.NoContent() : ApiErrors.Validation(error);
        });

        api.MapDelete("/settings/{name}", (string name, SettingsRegistry registry, SettingsStore store) => {
            var d = registry.Find(name);
            if (d == null) return ApiErrors.NotFound($"Налаштування {name} не існує");
            store.Reset(d);
            return Results.NoContent();
        });
        return api;
    }

    public sealed record SetSettingRequest(JsonElement Value);
}
