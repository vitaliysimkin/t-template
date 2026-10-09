using TTemplate.Api.Infrastructure;
using TTemplate.Api.Settings;
// #feature:tickets
using TTemplate.Api.Features.Tickets;
// #end
// #feature:jobs
using TTemplate.Api.Features.Jobs;
// #end

var builder = WebApplication.CreateBuilder(args);

// Ядро: тека даних, SQLite, налаштування (розділ «Загальне» — AppSettings).
builder.Services.AddAppCore(builder.Configuration, builder.Environment);
builder.Services.AddSettingsSection<AppSettings>();
// Фічі: кожна — свої сервіси, ендпоінти й розділ налаштувань (Features/<Фіча>).
// #feature:tickets
builder.Services.AddTickets();
// #end
// #feature:jobs
builder.Services.AddJobs();
// #end

var app = builder.Build();
app.Services.InitAppCore();

// Зібраний UI (npm run build → wwwroot при publish.ps1). У dev UI віддає Vite і проксіює /api сюди.
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapInfo();
api.MapSettings();
// #feature:tickets
api.MapTickets();
// #end
// #feature:jobs
api.MapJobs();
// #end
api.MapFallback(() => ApiErrors.NotFound("Невідомий маршрут API"));

app.MapFallbackToFile("index.html");
app.Run();
