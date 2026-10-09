using System.Collections.Concurrent;
using System.Text.Json;
using TTemplate.Api.Infrastructure;

namespace TTemplate.Api.Features.Jobs;

/// <summary>
/// Фонові операції: Start(...) одразу повертає Job, робота йде в окремому Task. Активні — у памʼяті,
/// кожна зміна стану (старт, крок, кінець) — у таблицю jobs (JSON), тож історія переживає перезапуск;
/// незавершені на момент зупинки при старті стають Interrupted.
/// </summary>
public sealed class JobRunner(Db db, ILogger<JobRunner> logger) : IStartupInit {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ConcurrentDictionary<string, (Job Job, CancellationTokenSource Cts)> _active = new();

    public Job Start(string type, string title, Func<JobContext, Task> work) {
        var job = new Job { Id = $"j-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}", Type = type, Title = title };
        var cts = new CancellationTokenSource();
        _active[job.Id] = (job, cts);
        Save(job);
        _ = Task.Run(() => Run(job, work, cts));
        return job;
    }

    private async Task Run(Job job, Func<JobContext, Task> work, CancellationTokenSource cts) {
        var ctx = new JobContext(job, () => Save(job), cts.Token);
        lock (job) { job.Status = JobStatus.Running; job.StartedAt = DateTimeOffset.Now; }
        Save(job);
        try {
            await work(ctx);
            ctx.FinishStep(StepStatus.Succeeded);
            lock (job) job.Status = JobStatus.Succeeded;
        } catch (OperationCanceledException) when (cts.IsCancellationRequested) {
            ctx.FinishStep(StepStatus.Cancelled);
            ctx.Log("Скасовано", "error");
            lock (job) job.Status = JobStatus.Cancelled;
        } catch (Exception e) {
            logger.LogError(e, "Операція {Id} ({Type}) впала", job.Id, job.Type);
            ctx.FinishStep(StepStatus.Failed);
            ctx.Log(e.Message, "error");
            lock (job) { job.Status = JobStatus.Failed; job.Error = e.Message; }
        } finally {
            lock (job) job.FinishedAt = DateTimeOffset.Now;
            Save(job);
            _active.TryRemove(job.Id, out _);
            cts.Dispose();
        }
    }

    public bool Cancel(string id) {
        if (!_active.TryGetValue(id, out var a)) return false;
        a.Cts.Cancel();
        return true;
    }

    public Job? Get(string id) {
        if (_active.TryGetValue(id, out var a)) return a.Job;
        using var c = db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Job>(json, Json) : null;
    }

    /// <summary>Останні операції (без логу — він лише в Get).</summary>
    public List<object> List(int limit, bool activeOnly) {
        var jobs = new List<Job>();
        if (activeOnly) {
            jobs.AddRange(_active.Values.Select(a => a.Job));
        } else {
            using var c = db.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT json FROM jobs ORDER BY created_at DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read()) {
                var j = JsonSerializer.Deserialize<Job>(r.GetString(0), Json)!;
                jobs.Add(_active.TryGetValue(j.Id, out var a) ? a.Job : j);
            }
        }
        return jobs.OrderByDescending(j => j.CreatedAt).Select(Summary).ToList();
    }

    private static object Summary(Job j) {
        lock (j) return new { j.Id, j.Type, j.Title, j.Status, j.CreatedAt, j.StartedAt, j.FinishedAt, j.Error, Steps = j.Steps.ToList() };
    }

    private void Save(Job job) {
        string json;
        lock (job) json = JsonSerializer.Serialize(job, Json);
        try {
            using var c = db.Open();
            Db.Exec(c, "INSERT INTO jobs(id, created_at, json) VALUES($id, $ts, $json) ON CONFLICT(id) DO UPDATE SET json = $json",
                ("$id", job.Id), ("$ts", job.CreatedAt.ToUnixTimeMilliseconds()), ("$json", json));
        } catch (Exception e) {
            logger.LogWarning(e, "Не вдалося зберегти операцію {Id}", job.Id);
        }
    }

    /// <summary>Старт застосунку: операції, що «бігли» до зупинки, — Interrupted.</summary>
    public void Init() {
        using var c = db.Open();
        var stale = new List<Job>();
        using (var cmd = c.CreateCommand()) {
            cmd.CommandText = "SELECT json FROM jobs WHERE json LIKE '%\"status\":\"running\"%' OR json LIKE '%\"status\":\"queued\"%'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) stale.Add(JsonSerializer.Deserialize<Job>(r.GetString(0), Json)!);
        }
        foreach (var j in stale.Where(j => j.IsActive)) {
            j.Status = JobStatus.Interrupted;
            j.FinishedAt ??= DateTimeOffset.Now;
            foreach (var s in j.Steps.Where(s => s.Status == StepStatus.Running)) s.Status = StepStatus.Cancelled;
            Save(j);
        }
    }
}
