using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TTemplate.Api.Infrastructure;

namespace TTemplate.Api.Settings;

public sealed record SettingsSectionInfo(Type Type, string Key, string Caption, string? Icon, int Order);

public sealed record SettingDefinition(SettingsSectionInfo Section, PropertyInfo Property, SettingAttribute Attr) {
    public string Name => $"{Section.Key}.{Property.Name}";

    public string Type => Property.PropertyType switch {
        var t when t == typeof(bool) => "bool",
        var t when t == typeof(int) => "int",
        var t when t == typeof(string[]) => "list",
        _ => "string",
    };
}

/// <summary>Розділи, зареєстровані через AddSettingsSection&lt;T&gt;.</summary>
public sealed class SettingsRegistry(IEnumerable<SettingsSectionInfo> sections) {
    public IReadOnlyList<SettingsSectionInfo> Sections { get; } = sections.OrderBy(s => s.Order).ToList();

    public IEnumerable<SettingDefinition> Definitions =>
        Sections.SelectMany(s => s.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p, a: p.GetCustomAttribute<SettingAttribute>()))
            .Where(x => x.a != null && x.p.CanWrite)
            .Select(x => new SettingDefinition(s, x.p, x.a!)));

    public SettingDefinition? Find(string name) => Definitions.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Значення налаштувань: таблиця settings(name, value) — лише змінені користувачем (JSON); решта — типові з класу.
/// Get&lt;T&gt;() — екземпляр розділу з урахуванням змін (кеш скидається при Set/Reset).
/// </summary>
public sealed class SettingsStore(Db db, SettingsRegistry registry, IDataProtectionProvider dp) {
    private const string SecretPrefix = "dp:";
    private readonly IDataProtector _protector = dp.CreateProtector("settings");
    private readonly ConcurrentDictionary<Type, object> _cache = new();

    public T Get<T>() where T : class, new() => (T)_cache.GetOrAdd(typeof(T), _ => Build(typeof(T)));

    private object Build(Type type) {
        var instance = Activator.CreateInstance(type)!;
        var stored = ReadAll();
        foreach (var d in registry.Definitions.Where(d => d.Section.Type == type))
            if (stored.TryGetValue(d.Name, out var raw))
                try { d.Property.SetValue(instance, Deserialize(d, raw)); } catch { /* пошкоджене значення — лишаємо типове */ }
        return instance;
    }

    public IEnumerable<object> Describe() {
        var stored = ReadAll();
        foreach (var d in registry.Definitions) {
            var defaults = Activator.CreateInstance(d.Section.Type)!;
            var custom = stored.TryGetValue(d.Name, out var raw);
            object? value = d.Property.GetValue(defaults);
            if (custom) try { value = Deserialize(d, raw!); } catch { custom = false; }
            yield return new {
                name = d.Name,
                section = d.Section.Key,
                group = d.Attr.Group,
                caption = d.Attr.Caption,
                description = d.Attr.Description,
                type = d.Type,
                secret = d.Attr.Secret,
                // Секрет назовні не віддаємо: лише чи задано.
                value = d.Attr.Secret ? null : value,
                defaultValue = d.Attr.Secret ? null : d.Property.GetValue(defaults),
                hasValue = d.Attr.Secret ? custom : (bool?)null,
                isCustomValue = custom,
            };
        }
    }

    /// <summary>Зберегти значення (JSON з UI). Повертає текст помилки валідації або null.</summary>
    public string? Set(SettingDefinition d, JsonElement value) {
        object? typed;
        try {
            typed = value.Deserialize(d.Property.PropertyType);
        } catch (JsonException) {
            return d.Type switch { "bool" => "Очікується так/ні", "int" => "Очікується ціле число", "list" => "Очікується список рядків", _ => "Очікується рядок" };
        }
        var json = JsonSerializer.Serialize(typed, d.Property.PropertyType);
        if (d.Attr.Secret) json = SecretPrefix + _protector.Protect(json);
        using (var c = db.Open())
            Db.Exec(c, "INSERT INTO settings(name, value) VALUES($n, $v) ON CONFLICT(name) DO UPDATE SET value = $v", ("$n", d.Name), ("$v", json));
        _cache.TryRemove(d.Section.Type, out _);
        return null;
    }

    public void Reset(SettingDefinition d) {
        using (var c = db.Open()) Db.Exec(c, "DELETE FROM settings WHERE name = $n", ("$n", d.Name));
        _cache.TryRemove(d.Section.Type, out _);
    }

    private object? Deserialize(SettingDefinition d, string raw) {
        if (raw.StartsWith(SecretPrefix)) raw = _protector.Unprotect(raw[SecretPrefix.Length..]);
        return JsonSerializer.Deserialize(raw, d.Property.PropertyType);
    }

    private Dictionary<string, string> ReadAll() {
        using var c = db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name, value FROM settings";
        using var r = cmd.ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (r.Read()) result[r.GetString(0)] = r.GetString(1);
        return result;
    }
}
