// Infrastructure/Repositories/CancellationRepository.cs
using Core.DTO.Cancellation;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;

namespace Infrastructure.Repositories
{
    public class CancellationRepository : ICancellationRepository
    {
        private readonly DbContextClass _db;

        public CancellationRepository(DbContextClass db)
        {
            _db = db;
        }

        public async Task<List<Cancellation>> GetAsync(CancellationFiltersDto filters)
        {
            var query = _db.Cancellations.AsQueryable();

            if (!string.IsNullOrEmpty(filters.Search))
            {
                var s = filters.Search.ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(s) ||
                    c.Reason.ToLower().Contains(s) ||
                    (c.Notes != null && c.Notes.ToLower().Contains(s)));
            }

            if (filters.CompanyId.HasValue)
                query = query.Where(c => c.CompanyId == filters.CompanyId.Value);

            if (filters.CustomerId.HasValue)
                query = query.Where(c => c.CustomerId == filters.CustomerId.Value);

            if (filters.StartDate.HasValue)
                query = query.Where(c => c.CancelledAt >= filters.StartDate.Value);

            if (filters.EndDate.HasValue)
                query = query.Where(c => c.CancelledAt <= filters.EndDate.Value);

            if (!string.IsNullOrEmpty(filters.RefundStatus))
                query = query.Where(c => c.RefundStatus.ToString() == filters.RefundStatus);

            return await query.ToListAsync();
        }

        public Task<Cancellation?> GetByIdAsync(int id)
            => _db.Cancellations.FirstOrDefaultAsync(c => c.Id == id);

        public Task AddAsync(Cancellation cancellation)
        {
            _db.Cancellations.Add(cancellation);
            return Task.CompletedTask;
        }

        public void Update(Cancellation cancellation)
            => _db.Cancellations.Update(cancellation);

        public void Delete(Cancellation cancellation)
            => _db.Cancellations.Remove(cancellation);
    }
}
public interface ICancellationRepository
{
    Task<List<Cancellation>> GetAsync(CancellationFiltersDto filters);
    Task<Cancellation?> GetByIdAsync(int id);
    Task AddAsync(Cancellation cancellation);
    void Update(Cancellation cancellation);
    void Delete(Cancellation cancellation);
}