namespace TTemplate.Api.Features.Jobs;

public enum JobStatus { Queued, Running, Succeeded, Failed, Cancelled, Interrupted }

public enum StepStatus { Pending, Running, Succeeded, Failed, Skipped, Cancelled }

public sealed class JobStep {
    public required string Title { get; init; }
    public StepStatus Status { get; set; } = StepStatus.Running;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed record JobLogLine(DateTimeOffset Ts, string Text, string Level = "info");

/// <summary>Операція: тип (для фільтра й іконки), заголовок, кроки, лог. Стан змінює лише JobRunner (під lock на самому Job).</summary>
public sealed class Job {
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
    public List<JobStep> Steps { get; init; } = [];
    public List<JobLogLine> Log { get; init; } = [];

    public bool IsActive => Status is JobStatus.Queued or JobStatus.Running;
}

/// <summary>Те, що бачить код операції: кроки, лог, скасування.</summary>
public sealed class JobContext(Job job, Action save, CancellationToken ct) {
    public CancellationToken CancellationToken { get; } = ct;

    /// <summary>Завершити поточний крок успіхом і почати новий.</summary>
    public void Step(string title) {
        lock (job) {
            FinishStep(StepStatus.Succeeded);
            job.Steps.Add(new JobStep { Title = title });
            job.Log.Add(new JobLogLine(DateTimeOffset.Now, title, "step"));
        }
        save();
    }

    public void Log(string text, string level = "info") {
        lock (job) job.Log.Add(new JobLogLine(DateTimeOffset.Now, text, level));
    }

    internal void FinishStep(StepStatus status) {
        lock (job) {
            var cur = job.Steps.LastOrDefault();
            if (cur is not { Status: StepStatus.Running }) return;
            cur.Status = status;
            cur.FinishedAt = DateTimeOffset.Now;
        }
    }
}
