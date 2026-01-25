using System;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.InternalFeedback;
using Core.Enums.InternalFeedback;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IInternalFeedbackRepository : IGenericRepository<InternalFeedback>
    {
        Task<PagedResult<InternalFeedback>> GetPagedAsync(InternalFeedbackFiltersDTO filters);
        Task<InternalFeedback?> GetByIdAsync(int id);
    }

    public class InternalFeedbackRepository
        : GenericRepository<InternalFeedback>,
          IInternalFeedbackRepository
    {
        private readonly DbContextClass _context;

        public InternalFeedbackRepository(DbContextClass context)
            : base(context)
        {
            _context = context;
        }

        public Task<InternalFeedback?> GetByIdAsync(int id)
        {
            return _context.InternalFeedbacks
                .Include(f => f.Comments)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<PagedResult<InternalFeedback>> GetPagedAsync(InternalFeedbackFiltersDTO filters)
        {
            var query = _context.InternalFeedbacks
                .Include(f => f.Comments)
                .AsQueryable();

            // Status filter
            if (!string.IsNullOrWhiteSpace(filters.Status)
                && !filters.Status.Equals("all", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<InternalFeedbackStatus>(filters.Status, true, out var statusEnum))
            {
                query = query.Where(f => f.Status == statusEnum);
            }

            // Priority filter
            if (!string.IsNullOrWhiteSpace(filters.Priority)
                && !filters.Priority.Equals("all", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<InternalFeedbackPriority>(filters.Priority, true, out var priorityEnum))
            {
                query = query.Where(f => f.Priority == priorityEnum);
            }

            // Category filter
            if (!string.IsNullOrWhiteSpace(filters.Category))
            {
                query = query.Where(f => f.Category == filters.Category);
            }

            // Company filter (via Professional.CompanyId) + Customer.CompanyId
if (filters.CompanyId.HasValue)
{
    var companyId = filters.CompanyId.Value;

    var professionalIds = _context.Professionals
        .Where(p => p.CompanyId == companyId)
        .Select(p => p.Id);

    var customerIds = _context.Customers
        .Where(c => c.CompanyId == companyId)
        .Select(c => c.Id);

    query = query.Where(f =>
        professionalIds.Contains(f.ProfessionalId) ||
        (f.CustomerId.HasValue && customerIds.Contains(f.CustomerId.Value)));
}

// Professional filter
            if (filters.ProfessionalId.HasValue)
            {
                query = query.Where(f => f.ProfessionalId == filters.ProfessionalId.Value);
            }

            // Team filter
            if (filters.TeamId.HasValue)
            {
                query = query.Where(f => f.TeamId == filters.TeamId.Value);
            }

            if (filters.CustomerId.HasValue)
            {
                query = query.Where(f => f.CustomerId == filters.CustomerId.Value);
            }

            if (filters.CustomerAddressId.HasValue)
            {
                query = query.Where(f => f.CustomerAddressId == filters.CustomerAddressId.Value);
            }

            if (filters.AppointmentId.HasValue)
            {
                query = query.Where(f => f.AppointmentId == filters.AppointmentId.Value);
            }

            // Text search
            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var txt = filters.Search.ToLower();
                query = query.Where(f =>
                    f.Title.ToLower().Contains(txt) ||
                    (f.Description ?? string.Empty).ToLower().Contains(txt));
            }

            // Order and paginate
            query = query.OrderByDescending(f => f.CreatedDate);
            return await query.GetPagedAsync(filters.PageNumber, filters.PageSize);
        }
    }
}
