using Core.DTO.Appointment;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class AppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
    {
        public AppointmentRepository(DbContextClass context) : base(context) { }

        public async Task<PagedResult<Appointment>> GetPagedAppointmentsAsync(AppointmentFiltersDTO filters)
        {
            var query = _dbContext.Set<Appointment>()
                .Include(a => a.Company)
                .Include(a => a.Customer)
                .Include(a => a.Team)
                .Include(a => a.Professional)
                .AsQueryable();

            // Filtros dinâmicos
            if (filters.CompanyId.HasValue)
                query = query.Where(a => a.CompanyId == filters.CompanyId.Value);

            if (filters.CustomerId.HasValue)
                query = query.Where(a => a.CustomerId == filters.CustomerId.Value);

            if (filters.TeamId.HasValue)
                query = query.Where(a => a.TeamId == filters.TeamId.Value);

            if (filters.ProfessionalId.HasValue)
                query = query.Where(a => a.ProfessionalId == filters.ProfessionalId.Value);

            if (filters.Status.HasValue)
                query = query.Where(a => a.Status == filters.Status.Value);

            if (filters.Type.HasValue)
                query = query.Where(a => a.Type == filters.Type.Value);

            if (!string.IsNullOrWhiteSpace(filters.Search))
                query = query.Where(a =>
                    a.Title.ToLower().Contains(filters.Search.ToLower()) ||
                    a.Address.ToLower().Contains(filters.Search.ToLower()));

            if (filters.StartDate.HasValue)
                query = query.Where(a => a.Start >= filters.StartDate.Value);

            if (filters.EndDate.HasValue)
                query = query.Where(a => a.End <= filters.EndDate.Value);

            query = query.OrderByDescending(a => a.Start);

            return await query.GetPagedAsync(filters.Page, filters.PageSize);
        }

        public async Task<List<Appointment>> GetAppointmentsByCompanyAsync(int companyId)
        {
            return await _dbContext.Set<Appointment>()
                .Include(a => a.Customer)
                .Include(a => a.Team)
                .Include(a => a.Professional)
                .Where(a => a.CompanyId == companyId)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetAppointmentsByTeamAsync(int teamId)
        {
            return await _dbContext.Set<Appointment>()
                .Include(a => a.Company)
                .Include(a => a.Customer)
                .Include(a => a.Professional)
                .Where(a => a.TeamId == teamId)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetAppointmentsByProfessionalAsync(int professionalId)
        {
            return await _dbContext.Set<Appointment>()
                .Include(a => a.Company)
                .Include(a => a.Customer)
                .Include(a => a.Team)
                .Where(a => a.ProfessionalId == professionalId)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetAppointmentsByCustomerAsync(int customerId)
        {
            return await _dbContext.Set<Appointment>()
                .Include(a => a.Company)
                .Include(a => a.Team)
                .Include(a => a.Professional)
                .Where(a => a.CustomerId == customerId)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetAppointmentsByDateRangeAsync(DateTime start, DateTime end, int? companyId = null)
        {
            var query = _dbContext.Set<Appointment>()
                .Include(a => a.Company)
                .Include(a => a.Customer)
                .Include(a => a.Team)
                .Include(a => a.Professional)
                .Where(a => a.Start >= start && a.End <= end);

            if (companyId.HasValue)
                query = query.Where(a => a.CompanyId == companyId.Value);

            return await query.ToListAsync();
        }
    }

    public interface IAppointmentRepository : IGenericRepository<Appointment>
    {
        Task<PagedResult<Appointment>> GetPagedAppointmentsAsync(AppointmentFiltersDTO filters);
        Task<List<Appointment>> GetAppointmentsByCompanyAsync(int companyId);
        Task<List<Appointment>> GetAppointmentsByTeamAsync(int teamId);
        Task<List<Appointment>> GetAppointmentsByProfessionalAsync(int professionalId);
        Task<List<Appointment>> GetAppointmentsByCustomerAsync(int customerId);
        Task<List<Appointment>> GetAppointmentsByDateRangeAsync(DateTime start, DateTime end, int? companyId = null);
    }
}
