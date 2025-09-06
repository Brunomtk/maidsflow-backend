using Core.DTO.GpsTracking;
using Core.Enums;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IGpsTrackingRepository : IGenericRepository<GpsTracking>
    {
        Task<GpsTracking?> GetByIdAsync(int id);
        Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters);
    }

    public class GpsTrackingRepository : GenericRepository<GpsTracking>, IGpsTrackingRepository
    {
        private readonly DbContextClass _context;

        public GpsTrackingRepository(DbContextClass context) : base(context)
            => _context = context;

        public async Task<GpsTracking?> GetByIdAsync(int id)
        {
            return await _context.GpsTrackings
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters)
        {
            var q = _context.GpsTrackings.AsQueryable();

            if (filters.Status.HasValue)
                q = q.Where(x => x.Status == filters.Status.Value);

            if (filters.CompanyId.HasValue)
                q = q.Where(x => x.CompanyId == filters.CompanyId.Value);

            if (filters.ProfessionalId.HasValue)
                q = q.Where(x => x.ProfessionalId == filters.ProfessionalId.Value);

            if (!string.IsNullOrWhiteSpace(filters.SearchQuery))
            {
                var txt = filters.SearchQuery.ToLower();
                q = q.Where(x =>
                    (!string.IsNullOrEmpty(x.ProfessionalName) && x.ProfessionalName.ToLower().Contains(txt))
                    || (!string.IsNullOrEmpty(x.Vehicle) && x.Vehicle.ToLower().Contains(txt))
                    || (!string.IsNullOrEmpty(x.Location.Address) && x.Location.Address.ToLower().Contains(txt)));
            }

            if (filters.DateFrom.HasValue)
                q = q.Where(x => x.Timestamp >= filters.DateFrom.Value);

            if (filters.DateTo.HasValue)
                q = q.Where(x => x.Timestamp <= filters.DateTo.Value);

            return await q
                .OrderByDescending(x => x.Timestamp)
                .GetPagedAsync(filters.PageNumber, filters.PageSize);
        }
    }
}
