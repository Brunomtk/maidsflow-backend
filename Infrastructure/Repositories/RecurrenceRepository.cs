using Core.DTO.Recurrences;
using Core.Enums.Recurrence;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IRecurrenceRepository : IGenericRepository<Recurrence>
    {
        Task<PagedResult<Recurrence>> GetPagedRecurrencesAsync(RecurrenceFiltersDTO filters);
    }

    public class RecurrenceRepository : GenericRepository<Recurrence>, IRecurrenceRepository
    {
        private readonly DbContextClass _context;

        public RecurrenceRepository(DbContextClass context) : base(context)
        {
            _context = context;
        }

        public async Task<PagedResult<Recurrence>> GetPagedRecurrencesAsync(RecurrenceFiltersDTO filters)
        {
            var query = _context.Recurrences
                .Include(r => r.Customer)
                .Include(r => r.Team)
                .Include(r => r.Company)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var term = filters.Search.ToLower();
                query = query.Where(r =>
                    (r.Title ?? "").ToLower().Contains(term) ||
                    (r.Address ?? "").ToLower().Contains(term) ||
                    (r.Notes ?? "").ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(filters.Status) &&
                Enum.TryParse<RecurrenceStatusEnum>(filters.Status, true, out var statusEnum))
            {
                query = query.Where(r => r.Status == statusEnum);
            }

            if (!string.IsNullOrWhiteSpace(filters.Type) &&
                Enum.TryParse<RecurrenceTypeEnum>(filters.Type, true, out var typeEnum))
            {
                query = query.Where(r => r.Type == typeEnum);
            }

            if (filters.CompanyId.HasValue)
                query = query.Where(r => r.CompanyId == filters.CompanyId.Value);

            if (filters.TeamId.HasValue)
                query = query.Where(r => r.TeamId == filters.TeamId.Value);

            if (filters.CustomerId.HasValue)
                query = query.Where(r => r.CustomerId == filters.CustomerId.Value);

            if (!string.IsNullOrWhiteSpace(filters.StartDate) &&
                DateTime.TryParse(filters.StartDate, out var startDate))
            {
                query = query.Where(r => r.StartDate >= startDate);
            }

            if (!string.IsNullOrWhiteSpace(filters.EndDate) &&
                DateTime.TryParse(filters.EndDate, out var endDate))
            {
                query = query.Where(r => r.StartDate <= endDate);
            }

            return await query
                .OrderByDescending(r => r.CreatedDate)
                .GetPagedAsync(filters.PageNumber, filters.PageSize);
        }
    }
}
