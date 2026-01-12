using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IAppointmentCompletionRepository : IGenericRepository<AppointmentCompletion>
    {
        Task<List<AppointmentCompletion>> GetByCompanyAndRangeAsync(int companyId, DateTime from, DateTime to);
        Task<AppointmentCompletion?> GetByAppointmentAndOccurrenceStartAsync(int appointmentId, DateTime occurrenceStart);
        Task<bool> ExistsAsync(int appointmentId, DateTime occurrenceStart);
    }

    public class AppointmentCompletionRepository : GenericRepository<AppointmentCompletion>, IAppointmentCompletionRepository
    {
        public AppointmentCompletionRepository(DbContextClass context) : base(context) { }

        public async Task<List<AppointmentCompletion>> GetByCompanyAndRangeAsync(int companyId, DateTime from, DateTime to)
        {
            // OccurrenceStart between [from, to]
            return await _dbContext.Set<AppointmentCompletion>()
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.OccurrenceStart >= from && x.OccurrenceStart <= to)
                .OrderBy(x => x.OccurrenceStart)
                .ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<AppointmentCompletion?> GetByAppointmentAndOccurrenceStartAsync(int appointmentId, DateTime occurrenceStart)
        {
            return await _dbContext.Set<AppointmentCompletion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId && x.OccurrenceStart == occurrenceStart);
        }

        public async Task<bool> ExistsAsync(int appointmentId, DateTime occurrenceStart)
        {
            return await _dbContext.Set<AppointmentCompletion>()
                .AsNoTracking()
                .AnyAsync(x => x.AppointmentId == appointmentId && x.OccurrenceStart == occurrenceStart);
        }
    }
}
