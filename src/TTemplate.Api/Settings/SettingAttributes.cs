namespace TTemplate.Api.Settings;

/// <summary>Клас налаштувань = розділ на екрані «Налаштування». Key — префікс імен ("general.AppTitle").</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SettingsSectionAttribute(string key, string caption) : Attribute {
    public string Key { get; } = key;
    public string Caption { get; } = caption;

    /// <summary>Іконка розділу (iconify-імʼя, напр. "system-uicons:flag").</summary>
    public string? Icon { get; init; }

    /// <summary>Порядок у навігації налаштувань (менше — вище).</summary>
    public int Order { get; init; } = 100;
}

/// <summary>
/// Властивість — налаштування. Типи: string, bool, int, string[] (список). Типове значення — ініціалізатор властивості.
/// Secret: значення не віддається в API і зберігається зашифрованим (ASP.NET Data Protection).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string caption) : Attribute {
    public string Caption { get; } = caption;
    public string? Description { get; init; }

    /// <summary>Підзаголовок усередині розділу (рядки з однаковою Group ідуть разом).</summary>
    public string? Group { get; init; }

    public bool Secret { get; init; }
}
