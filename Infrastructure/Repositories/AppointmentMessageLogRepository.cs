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

        // IMPORTANT: recurrence occurrences come from multiple sources (calendar, API, UI) and may differ by seconds/ticks.
        // So we match by a small time window instead of strict equality.
        if (occurrenceStartUtc.HasValue)
        {
            var start = EnsureUtc(occurrenceStartUtc.Value);
            var min = start.AddMinutes(-1);
            var max = start.AddMinutes(1);
            q = q.Where(x => x.OccurrenceStartUtc.HasValue && x.OccurrenceStartUtc.Value >= min && x.OccurrenceStartUtc.Value <= max);
        }

        if (occurrenceEndUtc.HasValue)
        {
            var end = EnsureUtc(occurrenceEndUtc.Value);
            var min = end.AddMinutes(-1);
            var max = end.AddMinutes(1);
            q = q.Where(x => x.OccurrenceEndUtc.HasValue && x.OccurrenceEndUtc.Value >= min && x.OccurrenceEndUtc.Value <= max);
        }

        return await q.OrderByDescending(x => x.CreatedDate).ToListAsync(ct);
    }

    public async Task<AppointmentMessageLog?> GetLatestAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var q = _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.Kind == kind && x.Channel == channel);

        if (occurrenceStartUtc.HasValue)
        {
            var start = EnsureUtc(occurrenceStartUtc.Value);
            var min = start.AddMinutes(-1);
            var max = start.AddMinutes(1);
            q = q.Where(x => x.OccurrenceStartUtc.HasValue && x.OccurrenceStartUtc.Value >= min && x.OccurrenceStartUtc.Value <= max);
        }

        if (occurrenceEndUtc.HasValue)
        {
            var end = EnsureUtc(occurrenceEndUtc.Value);
            var min = end.AddMinutes(-1);
            var max = end.AddMinutes(1);
            q = q.Where(x => x.OccurrenceEndUtc.HasValue && x.OccurrenceEndUtc.Value >= min && x.OccurrenceEndUtc.Value <= max);
        }

        return await q.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetNextAttemptAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, DateTime? occurrenceStartUtc = null, DateTime? occurrenceEndUtc = null, CancellationToken ct = default)
    {
        var q = _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.Kind == kind && x.Channel == channel);

        if (occurrenceStartUtc.HasValue)
        {
            var start = EnsureUtc(occurrenceStartUtc.Value);
            var min = start.AddMinutes(-1);
            var max = start.AddMinutes(1);
            q = q.Where(x => x.OccurrenceStartUtc.HasValue && x.OccurrenceStartUtc.Value >= min && x.OccurrenceStartUtc.Value <= max);
        }

        if (occurrenceEndUtc.HasValue)
        {
            var end = EnsureUtc(occurrenceEndUtc.Value);
            var min = end.AddMinutes(-1);
            var max = end.AddMinutes(1);
            q = q.Where(x => x.OccurrenceEndUtc.HasValue && x.OccurrenceEndUtc.Value >= min && x.OccurrenceEndUtc.Value <= max);
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
