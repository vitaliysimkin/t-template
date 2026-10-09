using TTemplate.Api.Infrastructure;

namespace TTemplate.Api.Features.Jobs;

/// <summary>
/// Фіча «Операції»: фонові задачі з кроками й логом (JobRunner), сторінка #/jobs і лічильник активних у бічній панелі.
/// Свою операцію запускай так: jobs.Start("import", "Імпорт файлу", async ctx => { ctx.Step("Читаю"); … }).
/// </summary>
public static class JobsFeature {
    public static IServiceCollection AddJobs(this IServiceCollection services) {
        services.AddSingleton<JobRunner>();
        services.AddSingleton<IStartupInit>(sp => sp.GetRequiredService<JobRunner>());
        services.AddDbMigration("""
            CREATE TABLE IF NOT EXISTS jobs(id TEXT PRIMARY KEY, created_at INTEGER NOT NULL, json TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS jobs_created ON jobs(created_at);
            """);
        return services;
    }

    public static RouteGroupBuilder MapJobs(this RouteGroupBuilder api) {
        api.MapGet("/jobs", (JobRunner jobs, int? limit, bool? active) =>
            Results.Ok(jobs.List(Math.Clamp(limit ?? 100, 1, 500), active == true)));

        api.MapGet("/jobs/{id}", (string id, JobRunner jobs) => {
            var job = jobs.Get(id);
            if (job == null) return ApiErrors.NotFound($"Операції {id} немає");
            lock (job) return Results.Json(new {
                job.Id, job.Type, job.Title, job.Status, job.CreatedAt, job.StartedAt, job.FinishedAt, job.Error,
                Steps = job.Steps.ToList(), Log = job.Log.ToList(),
            });
        });

        api.MapPost("/jobs/{id}/cancel", (string id, JobRunner jobs) =>
            jobs.Cancel(id) ? Results.NoContent() : ApiErrors.Error(409, "not_active", "Операція вже завершена"));

        // Демо: показує, як виглядає операція з кроками. Прибери, коли зʼявляться власні.
        api.MapPost("/jobs/demo", (JobRunner jobs) => Results.Ok(jobs.Start("demo", "Демо-операція", async ctx => {
            string[] steps = ["Підготовка", "Завантаження даних", "Обробка", "Збереження результату"];
            foreach (var step in steps) {
                ctx.Step(step);
                for (var i = 1; i <= 3; i++) {
                    await Task.Delay(500, ctx.CancellationToken);
                    ctx.Log($"{step}: {i * 33}%");
                }
            }
        })));
        return api;
    }
}
