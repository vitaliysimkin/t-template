namespace TTemplate.Api.Settings;

/// <summary>Розділ «Загальне». Додавай сюди налаштування застосунку; власні розділи — нові класи з [SettingsSection] + AddSettingsSection.</summary>
[SettingsSection("general", "Загальне", Icon = "system-uicons:sliders", Order = 0)]
public sealed class AppSettings {
    [Setting("Назва застосунку", Description = "Показується в бічній панелі й заголовку вкладки")]
    public string AppTitle { get; set; } = "TTemplate";

    [Setting("Ваше імʼя", Description = "Для картки в бічній панелі й підпису в заявках")]
    public string UserName { get; set; } = "Користувач";
}
