using System;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Review;
using Core.Enums;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ReviewRepository : GenericRepository<Review>, IReviewRepository
    {
        private readonly DbContextClass _context;

        public ReviewRepository(DbContextClass context) : base(context)
            => _context = context;

        public async Task<Review?> GetByIdAsync(int id)
        {
            return await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<PagedResult<Review>> GetPagedAsync(ReviewFiltersDTO filters)
        {
            var q = _context.Reviews.AsQueryable();

            // Filtros por status e rating
            if (!string.IsNullOrWhiteSpace(filters.Status)
                && !filters.Status.Equals("all", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<ReviewStatus>(filters.Status, true, out var statusEnum))
            {
                q = q.Where(x => x.Status == statusEnum);
            }

            if (!string.IsNullOrWhiteSpace(filters.Rating)
                && !filters.Rating.Equals("all", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(filters.Rating, out var ratingValue))
            {
                q = q.Where(x => x.Rating == ratingValue);
            }

            // Filtro por busca textual
            if (!string.IsNullOrWhiteSpace(filters.SearchQuery))
            {
                var txt = filters.SearchQuery.ToLower();
                q = q.Where(x =>
                    (x.CustomerName ?? "").ToLower().Contains(txt) ||
                    (x.ProfessionalName ?? "").ToLower().Contains(txt) ||
                    (x.CompanyName ?? "").ToLower().Contains(txt) ||
                    (x.Comment ?? "").ToLower().Contains(txt)
                );
            }

            // Filtros por IDs
            if (!string.IsNullOrWhiteSpace(filters.CustomerId))
                q = q.Where(x => x.CustomerId == filters.CustomerId);

            if (!string.IsNullOrWhiteSpace(filters.ProfessionalId))
                q = q.Where(x => x.ProfessionalId == filters.ProfessionalId);

            if (!string.IsNullOrWhiteSpace(filters.TeamId))
                q = q.Where(x => x.TeamId == filters.TeamId);

            if (!string.IsNullOrWhiteSpace(filters.CompanyId))
                q = q.Where(x => x.CompanyId == filters.CompanyId);

            if (!string.IsNullOrWhiteSpace(filters.AppointmentId))
                q = q.Where(x => x.AppointmentId == filters.AppointmentId);

            // Ordenação e paginação
            return await q
                .OrderByDescending(x => x.CreatedDate)
                .GetPagedAsync(filters.PageNumber, filters.PageSize);
        }
    }

    public interface IReviewRepository : IGenericRepository<Review>
    {
        Task<Review?> GetByIdAsync(int id);
        Task<PagedResult<Review>> GetPagedAsync(ReviewFiltersDTO filters);
    }
}
