using Core.DTO.GpsTracking;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IGpsTrackingRepository : IGenericRepository<GpsTracking>
    {
        Task<GpsTracking?> GetByIdAsync(int id);
        Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters);

        /// <summary>
        /// Lista pontos de GPS de um profissional dentro de um intervalo (UTC), ordenados por Timestamp ASC.
        /// </summary>
        Task<List<GpsTracking>> GetByProfessionalAndRangeAsync(int professionalId, DateTime utcFromInclusive, DateTime utcToExclusive, int? companyId = null);

        /// <summary>
        /// Retorna o último ponto (mais recente) de um profissional.
        /// </summary>
        Task<GpsTracking?> GetLastPointAsync(int professionalId, int? companyId = null);

        /// <summary>
        /// Remove pontos de GPS mais antigos que o threshold (UTC). Retorna quantidade deletada.
        /// </summary>
        Task<int> DeleteOlderThanAsync(DateTime utcThreshold);
    }

    public class GpsTrackingRepository : GenericRepository<GpsTracking>, IGpsTrackingRepository
    {
        private readonly DbContextClass _context;

        public GpsTrackingRepository(DbContextClass context) : base(context)
        {
            _context = context;
        }

        public async Task<GpsTracking?> GetByIdAsync(int id)
        {
            return await _context.GpsTrackings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters)
        {
            var q = _context.GpsTrackings.AsNoTracking().AsQueryable();

            if (filters.Status.HasValue)
                q = q.Where(x => x.Status == filters.Status.Value);

            if (filters.CompanyId.HasValue)
                q = q.Where(x => x.CompanyId == filters.CompanyId.Value);

            if (filters.ProfessionalId.HasValue)
                q = q.Where(x => x.ProfessionalId == filters.ProfessionalId.Value);

            if (filters.TeamId.HasValue)
                q = q.Where(x => x.TeamId == filters.TeamId.Value);

            if (!string.IsNullOrWhiteSpace(filters.SearchQuery))
            {
                var txt = filters.SearchQuery.ToLower();
                q = q.Where(x =>
                    (!string.IsNullOrEmpty(x.ProfessionalName) && x.ProfessionalName.ToLower().Contains(txt))
                    || (!string.IsNullOrEmpty(x.CompanyName) && x.CompanyName.ToLower().Contains(txt))
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

        public async Task<List<GpsTracking>> GetByProfessionalAndRangeAsync(int professionalId, DateTime utcFromInclusive, DateTime utcToExclusive, int? companyId = null)
        {
            var q = _context.GpsTrackings
                .AsNoTracking()
                .Where(x => x.ProfessionalId == professionalId)
                .Where(x => x.Timestamp >= utcFromInclusive && x.Timestamp < utcToExclusive);

            if (companyId.HasValue)
                q = q.Where(x => x.CompanyId == companyId.Value);

            return await q
                .OrderBy(x => x.Timestamp)
                .ToListAsync();
        }

        public async Task<GpsTracking?> GetLastPointAsync(int professionalId, int? companyId = null)
        {
            var q = _context.GpsTrackings
                .AsNoTracking()
                .Where(x => x.ProfessionalId == professionalId);

            if (companyId.HasValue)
                q = q.Where(x => x.CompanyId == companyId.Value);

            return await q
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task<int> DeleteOlderThanAsync(DateTime utcThreshold)
        {
            // EF Core 7+ suporta ExecuteDeleteAsync. Se não suportar, fallback para remoção em lote.
            try
            {
                return await _context.GpsTrackings
                    .Where(x => x.Timestamp < utcThreshold)
                    .ExecuteDeleteAsync();
            }
            catch
            {
                var old = await _context.GpsTrackings
                    .Where(x => x.Timestamp < utcThreshold)
                    .ToListAsync();

                if (old.Count == 0) return 0;
                _context.GpsTrackings.RemoveRange(old);
                await _context.SaveChangesAsync();
                return old.Count;
            }
        }
    }
}
