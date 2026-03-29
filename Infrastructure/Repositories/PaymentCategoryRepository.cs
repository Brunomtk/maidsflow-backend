using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IPaymentCategoryRepository
    {
        Task<List<PaymentCategory>> GetByCompanyIdAsync(int companyId, bool includeInactive = false);
        Task<PaymentCategory?> GetByIdAsync(int id);
        Task<PaymentCategory?> GetByCompanyIdAndNameAsync(int companyId, string name);
        Task<bool> HasPaymentsAsync(int paymentCategoryId);
        void Add(PaymentCategory entity);
        void Update(PaymentCategory entity);
        void Delete(PaymentCategory entity);
    }

    public class PaymentCategoryRepository : IPaymentCategoryRepository
    {
        private readonly DbContextClass _dbContext;

        public PaymentCategoryRepository(DbContextClass dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<List<PaymentCategory>> GetByCompanyIdAsync(int companyId, bool includeInactive = false)
        {
            var query = _dbContext.Set<PaymentCategory>().Where(x => x.CompanyId == companyId);
            if (!includeInactive)
                query = query.Where(x => x.Active);
            return query.OrderBy(x => x.Name).ToListAsync();
        }

        public Task<PaymentCategory?> GetByIdAsync(int id)
            => _dbContext.Set<PaymentCategory>().FirstOrDefaultAsync(x => x.Id == id);

        public Task<PaymentCategory?> GetByCompanyIdAndNameAsync(int companyId, string name)
            => _dbContext.Set<PaymentCategory>().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Name.ToLower() == name.ToLower());

        public Task<bool> HasPaymentsAsync(int paymentCategoryId)
            => _dbContext.Payments.AnyAsync(x => x.PaymentCategoryId == paymentCategoryId);

        public void Add(PaymentCategory entity) => _dbContext.Set<PaymentCategory>().Add(entity);
        public void Update(PaymentCategory entity) => _dbContext.Set<PaymentCategory>().Update(entity);
        public void Delete(PaymentCategory entity) => _dbContext.Set<PaymentCategory>().Remove(entity);
    }
}
