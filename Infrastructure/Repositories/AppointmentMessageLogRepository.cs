using Core.Enums.Messaging;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public interface IAppointmentMessageLogRepository : IGenericRepository<AppointmentMessageLog>
{
    Task<List<AppointmentMessageLog>> GetByAppointmentAsync(int appointmentId, CancellationToken ct = default);
    Task<AppointmentMessageLog?> GetLatestAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, CancellationToken ct = default);
    Task<int> GetNextAttemptAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, CancellationToken ct = default);
}

public class AppointmentMessageLogRepository : GenericRepository<AppointmentMessageLog>, IAppointmentMessageLogRepository
{
    public AppointmentMessageLogRepository(DbContextClass context) : base(context) { }

    public async Task<List<AppointmentMessageLog>> GetByAppointmentAsync(int appointmentId, CancellationToken ct = default)
    {
        return await _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(ct);
    }

    public async Task<AppointmentMessageLog?> GetLatestAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, CancellationToken ct = default)
    {
        return await _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.Kind == kind && x.Channel == channel)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetNextAttemptAsync(int appointmentId, AppointmentMessageKind kind, AppointmentMessageChannel channel, CancellationToken ct = default)
    {
        var last = await _dbContext.AppointmentMessageLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.Kind == kind && x.Channel == channel)
            .OrderByDescending(x => x.Attempt)
            .Select(x => x.Attempt)
            .FirstOrDefaultAsync(ct);

        return (last <= 0 ? 1 : last + 1);
    }
}
