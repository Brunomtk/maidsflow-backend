using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IPayrollItemRepository : IGenericRepository<PayrollItem>
    {
        Task<List<PayrollItem>> GetByRunIdAsync(int payrollRunId);
        Task<int> CountMissingRulesAsync(int payrollRunId);
        Task<decimal> SumCalculatedAmountAsync(int payrollRunId);
    }

    public class PayrollItemRepository : GenericRepository<PayrollItem>, IPayrollItemRepository
    {
        public PayrollItemRepository(DbContextClass context) : base(context) { }

        public async Task<List<PayrollItem>> GetByRunIdAsync(int payrollRunId)
        {
            return await _dbContext.Set<PayrollItem>()
                .AsNoTracking()
                .Include(i => i.Professional)
                .Include(i => i.Appointment).ThenInclude(a => a.Customer)
                .Include(i => i.ServiceType)
                .Where(i => i.PayrollRunId == payrollRunId)
                .OrderBy(i => i.OccurrenceStart)
                .ThenBy(i => i.ProfessionalId)
                .ToListAsync();
        }

        public async Task<int> CountMissingRulesAsync(int payrollRunId)
        {
            return await _dbContext.Set<PayrollItem>()
                .AsNoTracking()
                .Where(i => i.PayrollRunId == payrollRunId && i.MissingRule)
                .CountAsync();
        }

        public async Task<decimal> SumCalculatedAmountAsync(int payrollRunId)
        {
            return await _dbContext.Set<PayrollItem>()
                .AsNoTracking()
                .Where(i => i.PayrollRunId == payrollRunId)
                .Select(i => i.CalculatedAmount)
                .SumAsync();
        }
    }
}
