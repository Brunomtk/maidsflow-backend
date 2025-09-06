using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Core.DTO.CheckRecord;
using Core.Enums.CheckRecord;
using Core.Models;

namespace Infrastructure.Repositories
{
    public interface ICheckRecordRepository : IGenericRepository<CheckRecord>
    {
        Task<PagedResult<CheckRecord>> GetPagedAsync(CheckRecordFiltersDTO filters);
        Task<CheckRecord?> GetByIdAsync(int id);
    }

    public class CheckRecordRepository : GenericRepository<CheckRecord>, ICheckRecordRepository
    {
        private readonly DbContextClass _context;

        public CheckRecordRepository(DbContextClass context) : base(context)
        {
            _context = context;
        }

        public async Task<CheckRecord?> GetByIdAsync(int id)
        {
            return await _context.CheckRecords.FindAsync(id);
        }

        public async Task<PagedResult<CheckRecord>> GetPagedAsync(CheckRecordFiltersDTO filters)
        {
            var query = _context.CheckRecords.AsQueryable();

            if (filters.ProfessionalId.HasValue)
                query = query.Where(x => x.ProfessionalId == filters.ProfessionalId.Value);

            if (filters.CompanyId.HasValue)
                query = query.Where(x => x.CompanyId == filters.CompanyId.Value);

            if (filters.CustomerId.HasValue)
                query = query.Where(x => x.CustomerId == filters.CustomerId.Value);

            if (filters.TeamId.HasValue)
                query = query.Where(x => x.TeamId == filters.TeamId.Value);

            if (filters.AppointmentId.HasValue)
                query = query.Where(x => x.AppointmentId == filters.AppointmentId.Value);

            if (!string.IsNullOrEmpty(filters.Status) &&
                Enum.TryParse<CheckRecordStatus>(filters.Status, true, out var statusEnum))
            {
                query = query.Where(x => x.Status == statusEnum);
            }

            if (!string.IsNullOrEmpty(filters.ServiceType))
                query = query.Where(x => x.ServiceType.ToLower().Contains(filters.ServiceType.ToLower()));

            if (!string.IsNullOrEmpty(filters.StartDate) &&
                DateTime.TryParse(filters.StartDate, out var startDate))
            {
                query = query.Where(x => x.CreatedDate >= startDate);
            }

            if (!string.IsNullOrEmpty(filters.EndDate) &&
                DateTime.TryParse(filters.EndDate, out var endDate))
            {
                query = query.Where(x => x.CreatedDate <= endDate);
            }

            if (!string.IsNullOrEmpty(filters.Search))
            {
                var search = filters.Search.ToLower();
                query = query.Where(x =>
                    (x.ProfessionalName != null && x.ProfessionalName.ToLower().Contains(search)) ||
                    (x.CustomerName != null && x.CustomerName.ToLower().Contains(search)) ||
                    (x.TeamName != null && x.TeamName.ToLower().Contains(search)) ||
                    x.Address.ToLower().Contains(search) ||
                    x.ServiceType.ToLower().Contains(search) ||
                    (x.Notes != null && x.Notes.ToLower().Contains(search))
                );
            }

            return await query
                .OrderByDescending(x => x.CreatedDate)
                .GetPagedAsync(filters.PageNumber, filters.PageSize);
        }
    }
}
