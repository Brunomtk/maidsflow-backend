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
        Task DeleteByRunIdAsync(int payrollRunId);
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

        public async Task DeleteByRunIdAsync(int payrollRunId)
        {
            // IMPORTANT:
            // Do NOT load PayrollItems with Includes and then Remove() them one by one.
            // Items may reference the same Appointment; AsNoTracking doesn't do identity resolution by default,
            // so you'd end up attaching multiple Appointment instances with the same key and EF will throw:
            // "The instance of entity type 'Appointment' cannot be tracked because another instance with the same key value is already being tracked"
            // Bulk delete avoids tracking the graph entirely.
            await _dbContext.Set<PayrollItem>()
                .Where(i => i.PayrollRunId == payrollRunId)
                .ExecuteDeleteAsync();
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
