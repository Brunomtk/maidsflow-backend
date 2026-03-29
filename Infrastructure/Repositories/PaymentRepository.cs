using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Payments;
using Core.Enums;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly DbContextClass _dbContext;

        public PaymentRepository(DbContextClass dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<PagedResult<Payment>> GetPagedAsync(PaymentFiltersDto filters)
        {
            var query = _dbContext.Payments.AsQueryable();

            if (filters.CompanyId.HasValue)
                query = query.Where(p => p.CompanyId == filters.CompanyId.Value);

            if (filters.CustomerId.HasValue)
                query = query.Where(p => p.CustomerId == filters.CustomerId.Value);

            if (filters.CustomerAddressId.HasValue)
                query = query.Where(p => p.CustomerAddressId == filters.CustomerAddressId.Value);

            if (filters.Status.HasValue)
                query = query.Where(p => p.Status == filters.Status.Value);

            if (!string.IsNullOrEmpty(filters.Search))
                query = query.Where(p => p.Reference.Contains(filters.Search!));

            if (filters.StartDate.HasValue)
                query = query.Where(p => p.DueDate >= filters.StartDate.Value);

            if (filters.EndDate.HasValue)
                query = query.Where(p => p.DueDate <= filters.EndDate.Value);

            if (filters.PlanId.HasValue)
                query = query.Where(p => p.PlanId == filters.PlanId.Value);

            if (filters.FinancialType.HasValue)
                query = query.Where(p => p.FinancialType == filters.FinancialType.Value);

            if (filters.PaymentCategoryId.HasValue)
                query = query.Where(p => p.PaymentCategoryId == filters.PaymentCategoryId.Value);

            if (!string.IsNullOrWhiteSpace(filters.PaymentCategoryName))
                query = query.Where(p => p.PaymentCategoryName != null && p.PaymentCategoryName.ToLower().Contains(filters.PaymentCategoryName.ToLower()));

            return query.OrderByDescending(p => p.DueDate).GetPagedAsync(filters.Page, filters.PageSize);
        }

        public Task<Payment?> GetByIdAsync(int id)
            => _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == id);

        public Task<List<Payment>> GetByCustomerIdAsync(int customerId)
            => _dbContext.Payments
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.DueDate)
                .ToListAsync();

        public void Add(Payment entity)
            => _dbContext.Payments.Add(entity);

        public void Update(Payment entity)
            => _dbContext.Payments.Update(entity);

        public void Delete(Payment entity)
            => _dbContext.Payments.Remove(entity);
    }
    public interface IPaymentRepository
    {
        Task<PagedResult<Payment>> GetPagedAsync(PaymentFiltersDto filters);
        Task<Payment?> GetByIdAsync(int id);
        Task<List<Payment>> GetByCustomerIdAsync(int customerId);
        void Add(Payment entity);
        void Update(Payment entity);
        void Delete(Payment entity);
    }
}