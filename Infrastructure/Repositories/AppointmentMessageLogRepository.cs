using System;
using Core.Enums.Messaging;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public interface IAppointmentMessageLogRepository : IGenericRepository<AppointmentMessageLog>
{
    Task<List<AppointmentMessageLog>> GetByAppointmentAsync(int appointmentId, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default);
    Task<AppointmentMessageLog?> GetLatestAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default);
    Task<int> GetNextAttemptAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default);
}

public class AppointmentMessageLogRepository : GenericRepository<AppointmentMessageLog>, IAppointmentMessageLogRepository
{
    public AppointmentMessageLogRepository(DbContextClass context) : base(context) { }

    public async Task<List<AppointmentMessageLog>> GetByAppointmentAsync(int appointmentId, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var q = _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId);
        // IMPORTANT: recurrence occurrences can come from multiple sources and may differ slightly.
        // Match by tolerance + overlap instead of strict equality.
        if (occurrenceStartUtc.HasValue || occurrenceEndUtc.HasValue)
        {
            var tol = TimeSpan.FromMinutes(5);
DateTime? qs = occurrenceStartUtc.HasValue ? EnsureUtc(occurrenceStartUtc.Value) : null;
DateTime? qe = occurrenceEndUtc.HasValue ? EnsureUtc(occurrenceEndUtc.Value) : null;
DateTime? qsMin = qs.HasValue ? qs.Value - tol : null;
DateTime? qsMax = qs.HasValue ? qs.Value + tol : null;
DateTime? qeMin = qe.HasValue ? qe.Value - tol : null;
DateTime? qeMax = qe.HasValue ? qe.Value + tol : null;

q = q.Where(x =>
    (!qs.HasValue && !qe.HasValue)
    ||
    (x.OccurrenceStartUtc.HasValue && x.OccurrenceEndUtc.HasValue &&
        (
            (qs.HasValue && x.OccurrenceStartUtc.Value >= qsMin!.Value && x.OccurrenceStartUtc.Value <= qsMax!.Value)
            ||
            (qe.HasValue && x.OccurrenceEndUtc.Value >= qeMin!.Value && x.OccurrenceEndUtc.Value <= qeMax!.Value)
            ||
            (qs.HasValue && qe.HasValue && x.OccurrenceStartUtc.Value <= qeMax!.Value && x.OccurrenceEndUtc.Value >= qsMin!.Value)
        )
    )
);

        }

        return await q.OrderByDescending(x => x.CreatedDate).ToListAsync(ct);
    }

    public async Task<AppointmentMessageLog?> GetLatestAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var q = _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.Kind == kind && x.Channel == channel);
        if (occurrenceStartUtc.HasValue || occurrenceEndUtc.HasValue)
        {
            var tol = TimeSpan.FromMinutes(5);
DateTime? qs = occurrenceStartUtc.HasValue ? EnsureUtc(occurrenceStartUtc.Value) : null;
DateTime? qe = occurrenceEndUtc.HasValue ? EnsureUtc(occurrenceEndUtc.Value) : null;
DateTime? qsMin = qs.HasValue ? qs.Value - tol : null;
DateTime? qsMax = qs.HasValue ? qs.Value + tol : null;
DateTime? qeMin = qe.HasValue ? qe.Value - tol : null;
DateTime? qeMax = qe.HasValue ? qe.Value + tol : null;

q = q.Where(x =>
    (!qs.HasValue && !qe.HasValue)
    ||
    (x.OccurrenceStartUtc.HasValue && x.OccurrenceEndUtc.HasValue &&
        (
            (qs.HasValue && x.OccurrenceStartUtc.Value >= qsMin!.Value && x.OccurrenceStartUtc.Value <= qsMax!.Value)
            ||
            (qe.HasValue && x.OccurrenceEndUtc.Value >= qeMin!.Value && x.OccurrenceEndUtc.Value <= qeMax!.Value)
            ||
            (qs.HasValue && qe.HasValue && x.OccurrenceStartUtc.Value <= qeMax!.Value && x.OccurrenceEndUtc.Value >= qsMin!.Value)
        )
    )
);

        }

        return await q.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetNextAttemptAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var q = _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.Kind == kind && x.Channel == channel);
        if (occurrenceStartUtc.HasValue || occurrenceEndUtc.HasValue)
        {
            var tol = TimeSpan.FromMinutes(5);
DateTime? qs = occurrenceStartUtc.HasValue ? EnsureUtc(occurrenceStartUtc.Value) : null;
DateTime? qe = occurrenceEndUtc.HasValue ? EnsureUtc(occurrenceEndUtc.Value) : null;
DateTime? qsMin = qs.HasValue ? qs.Value - tol : null;
DateTime? qsMax = qs.HasValue ? qs.Value + tol : null;
DateTime? qeMin = qe.HasValue ? qe.Value - tol : null;
DateTime? qeMax = qe.HasValue ? qe.Value + tol : null;

q = q.Where(x =>
    (!qs.HasValue && !qe.HasValue)
    ||
    (x.OccurrenceStartUtc.HasValue && x.OccurrenceEndUtc.HasValue &&
        (
            (qs.HasValue && x.OccurrenceStartUtc.Value >= qsMin!.Value && x.OccurrenceStartUtc.Value <= qsMax!.Value)
            ||
            (qe.HasValue && x.OccurrenceEndUtc.Value >= qeMin!.Value && x.OccurrenceEndUtc.Value <= qeMax!.Value)
            ||
            (qs.HasValue && qe.HasValue && x.OccurrenceStartUtc.Value <= qeMax!.Value && x.OccurrenceEndUtc.Value >= qsMin!.Value)
        )
    )
);

        }

        var last = await q
            .OrderByDescending(x => x.Attempt)
            .Select(x => x.Attempt)
            .FirstOrDefaultAsync(ct);

        return (last <= 0 ? 1 : last + 1);
    }

    private static DateTime EnsureUtc(DateTime dt)
        => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
