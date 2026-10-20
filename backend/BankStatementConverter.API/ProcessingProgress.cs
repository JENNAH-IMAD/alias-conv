using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.API;

public sealed record ProcessingStatus(string Stage, int Percent, DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt, int? ExtractedPages = null, int? TotalPages = null, string ActiveStage = "checking");

// Short-lived, bounded metadata only. Financial data, filenames and provider credentials never enter this store.
public sealed class ProcessingProgressStore(TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly Dictionary<Guid, (string Owner, ProcessingStatus Status)> jobs = new();
    public void Start(Guid id, string owner)
    {
        lock (gate)
        {
            var now = time.GetUtcNow();
            foreach (var expired in jobs.Where(j => now - j.Value.Status.UpdatedAt > TimeSpan.FromMinutes(30)).Select(j => j.Key).ToArray())
                jobs.Remove(expired);
            if (jobs.ContainsKey(id))
                throw new AppException(409, "Ce traitement a déjà été démarré. Relancez l’import avec une nouvelle demande.");
            // Completed history must not prevent new users from converting documents.
            if (jobs.Count >= 512)
            {
                var completed = jobs.Where(j => j.Value.Status.Stage is "done" or "failed" or "cancelled")
                    .OrderBy(j => j.Value.Status.UpdatedAt).FirstOrDefault();
                if (completed.Key != Guid.Empty)
                    jobs.Remove(completed.Key);
            }
            if (jobs.Count >= 512)
                throw new AppException(429, "Le suivi des traitements est occupé. Réessayez dans quelques instants.");
            jobs[id] = (owner, new("checking", 10, now, now));
        }
    }
    public ProcessingStatus Get(Guid id, string owner)
    {
        lock (gate)
        {
            if (!jobs.TryGetValue(id, out var job) || job.Owner != owner || time.GetUtcNow() - job.Status.UpdatedAt > TimeSpan.FromMinutes(30))
                throw new AppException(404, "Suivi du traitement indisponible.");
            return job.Status;
        }
    }
    public void Report(Guid id, string stage, int percent, int? extractedPages, int? totalPages)
    {
        lock (gate)
        {
            if (!jobs.TryGetValue(id, out var job) || job.Status.Stage is "done" or "failed" or "cancelled")
                return;
            var status = job.Status;
            jobs[id] = (job.Owner, status with
            {
                Stage = stage,
                Percent = Math.Max(status.Percent, Math.Clamp(percent, 0, stage == "done" ? 100 : 99)),
                ActiveStage = stage is "done" or "failed" or "cancelled" ? status.ActiveStage : stage,
                UpdatedAt = time.GetUtcNow(),
                ExtractedPages = extractedPages.HasValue ? Math.Max(status.ExtractedPages ?? 0, extractedPages.Value) : status.ExtractedPages,
                TotalPages = totalPages ?? status.TotalPages
            });
        }
    }
}

public sealed class RequestProcessingProgress(ProcessingProgressStore store) : IProcessingProgress
{
    private Guid? id;
    public void Start(Guid processingId, string owner)
    {
        store.Start(processingId, owner);
        id = processingId;
    }
    public void Report(string stage, int percent, int? extractedPages = null, int? totalPages = null)
    {
        if (id.HasValue)
            store.Report(id.Value, stage, percent, extractedPages, totalPages);
    }
}
