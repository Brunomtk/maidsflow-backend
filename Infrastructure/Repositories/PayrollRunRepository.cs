using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IPayrollRunRepository : IGenericRepository<PayrollRun>
    {
        Task<List<PayrollRun>> GetByCompanyAsync(int companyId);
        Task<PayrollRun?> GetByIdWithItemsAsync(int id);
    }

    public class PayrollRunRepository : GenericRepository<PayrollRun>, IPayrollRunRepository
    {
        public PayrollRunRepository(DbContextClass context) : base(context) { }

        public async Task<List<PayrollRun>> GetByCompanyAsync(int companyId)
        {
            return await _dbContext.Set<PayrollRun>()
                .AsNoTracking()
                .Where(r => r.CompanyId == companyId)
                .OrderByDescending(r => r.PeriodStart)
                .ThenByDescending(r => r.Id)
                .ToListAsync();
        }

        public async Task<PayrollRun?> GetByIdWithItemsAsync(int id)
        {
            return await _dbContext.Set<PayrollRun>()
                .Include(r => r.Items)
                    .ThenInclude(i => i.Professional)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Appointment)
                        .ThenInclude(a => a.Customer)
                .Include(r => r.Items)
                    .ThenInclude(i => i.ServiceType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }
    }
}
