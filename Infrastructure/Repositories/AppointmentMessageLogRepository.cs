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
            q = q.Where(x => OccurrenceMatches(x, occurrenceStartUtc, occurrenceEndUtc));
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
            q = q.Where(x => OccurrenceMatches(x, occurrenceStartUtc, occurrenceEndUtc));
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
            q = q.Where(x => OccurrenceMatches(x, occurrenceStartUtc, occurrenceEndUtc));
        }

        var last = await q
            .OrderByDescending(x => x.Attempt)
            .Select(x => x.Attempt)
            .FirstOrDefaultAsync(ct);

        return (last <= 0 ? 1 : last + 1);
    }

    

    private static bool OccurrenceMatches(AppointmentMessageLog x, DateTime? occurrenceStartUtc, DateTime? occurrenceEndUtc)
    {
        // Recurrence timestamps can vary slightly across sources (seconds/ticks/timezone conversions).
        // Match using tolerance + interval overlap.
        var tol = TimeSpan.FromMinutes(5);

        if (occurrenceStartUtc.HasValue && occurrenceEndUtc.HasValue)
        {
            var qs = EnsureUtc(occurrenceStartUtc.Value);
            var qe = EnsureUtc(occurrenceEndUtc.Value);
            if (!x.OccurrenceStartUtc.HasValue || !x.OccurrenceEndUtc.HasValue) return false;
            var xs = EnsureUtc(x.OccurrenceStartUtc.Value);
            var xe = EnsureUtc(x.OccurrenceEndUtc.Value);
            return xs <= qe.Add(tol) && xe >= qs.Add(-tol);
        }

        if (occurrenceStartUtc.HasValue)
        {
            var qs = EnsureUtc(occurrenceStartUtc.Value);
            var min = qs.Add(-tol);
            var max = qs.Add(tol);
            return x.OccurrenceStartUtc.HasValue && EnsureUtc(x.OccurrenceStartUtc.Value) >= min && EnsureUtc(x.OccurrenceStartUtc.Value) <= max;
        }

        if (occurrenceEndUtc.HasValue)
        {
            var qe = EnsureUtc(occurrenceEndUtc.Value);
            var min = qe.Add(-tol);
            var max = qe.Add(tol);
            return x.OccurrenceEndUtc.HasValue && EnsureUtc(x.OccurrenceEndUtc.Value) >= min && EnsureUtc(x.OccurrenceEndUtc.Value) <= max;
        }

        return true;
    }
private static DateTime EnsureUtc(DateTime dt)
        => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
