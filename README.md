# TTemplate

Pet-project, створений із шаблону T-Template: .NET 10 (minimal API + SQLite) і Vue 3 + [t-components](https://www.npmjs.com/package/@vitaliysimkin/t-components).

## Запуск у dev

Потрібні .NET SDK 10 і Node 22+.

```bash
dotnet run --project src/TTemplate.Api --launch-profile http
```

```bash
npm run dev --prefix src/TTemplate.UI
```

UI — http://localhost:5173 (Vite проксіює `/api` на бекенд http://localhost:5180). Дані в dev — `data/` у корені репо (у git не потрапляє).

## Збірка

```bash
./publish.ps1
```

Результат — `publish/`: бекенд разом з UI у `wwwroot`. Запуск: `publish/TTemplate.Api --urls http://0.0.0.0:5180`.

## Структура

```
src/TTemplate.Api/
  Program.cs                 підключення ядра й фіч
  Infrastructure/            тека даних, SQLite (Db + міграції), формат помилок API
  Settings/                  налаштування на атрибутах: AppSettings (розділ «Загальне»), сховище, /api/settings
  Features/<Фіча>/           самодостатні фічі: сервіси, ендпоінти, свій розділ налаштувань
src/TTemplate.UI/src/
  App.vue                    оболонка: TSidebar / TBottomNav, вікно «Налаштування» (?settings=…), компоненти фіч поверх сторінок
  features/index.ts          реєстр фіч (меню, маршрути, overlays)
  features/<фіча>/           UI фічі
  components/settings/       панель налаштувань (вікно й сторінка)
  views/                     сторінки ядра (Головна, Налаштування)
```

## Як додати…

**Сторінку.** Створи `src/views/MyView.vue` (`<section class="page">` + `<header class="page-head">`), додай маршрут у `router.ts`, а пункт меню — у `menuItems` в `App.vue`.

**Налаштування.** Додай властивість з `[Setting("Підпис", Description = "…")]` в `AppSettings` (розділ «Загальне») або створи свій клас:

```csharp
[SettingsSection("mail", "Пошта", Icon = "system-uicons:mail", Order = 20)]
public sealed class MailSettings {
    [Setting("SMTP-сервер")] public string Host { get; set; } = "";
    [Setting("Пароль", Secret = true)] public string Password { get; set; } = "";
}
// Program.cs: builder.Services.AddSettingsSection<MailSettings>();
```

Розділ сам зʼявиться в UI. Значення читай через інжект `MailSettings` (актуальне на момент запиту) або `SettingsStore.Get<MailSettings>()`.
Типи: `string`, `bool`, `int`, `string[]`. Секрети зберігаються зашифрованими (ASP.NET Data Protection, ключі в `data/keys`).

**Фічу.** Бекенд: `Features/X/XFeature.cs` з `AddX()` / `MapX()`, підключи в `Program.cs`. UI: `src/features/x/index.ts`, що експортує `AppFeature` (меню, маршрути, overlays, start), і рядок у `features/index.ts`.

**Таблицю.** `services.AddDbMigration("CREATE TABLE IF NOT EXISTS …")` в `AddX()`, а потім `Db.Open()` з Microsoft.Data.Sqlite.

<!-- #feature:jobs -->
## Операції (jobs)

Фонові задачі з кроками й логом: сторінка `#/jobs`, лічильник активних у меню, drawer з деталями. Історія зберігається в SQLite.

```csharp
app.MapPost("/api/import", (JobRunner jobs) => jobs.Start("import", "Імпорт", async ctx => {
    ctx.Step("Читаю файл");
    ctx.Log("100 рядків");
    await Task.Delay(1000, ctx.CancellationToken);
    ctx.Step("Зберігаю");
}));
```

UI: `track(http.post<Job>('/api/import'))` з `features/jobs/state.ts` показує операцію одразу. Підпис типу додай у `jobTypeLabel`.
Демо-ендпоінт `/api/jobs/demo` і кнопку «Демо-операція» прибери, коли зʼявляться власні операції.
<!-- #end -->

<!-- #feature:tickets -->
## Заявки (tickets)

Плаваюча кнопка з прапорцем (або Ctrl+Shift+E): вибираєш елемент сторінки й описуєш, що не так. Заявка разом зі знімком сторінки потрапляє в `data/tickets/<id>/`.
Далі в Claude Code кажеш «опрацюй заявки» (скіл `.claude/skills/tickets`). Claude пише варіанти (HTML) або фікс, ти дивишся їх на `#/tickets/<id>`, погоджуєш, і Claude вносить зміни в код.
Формат файлів описано в [docs/tickets.md](docs/tickets.md).
<!-- #end -->
