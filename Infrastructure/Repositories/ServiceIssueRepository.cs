using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IServiceIssueRepository : IGenericRepository<ServiceIssue>
    {
        Task<ServiceIssue?> GetByIdAsync(int id);
        Task<List<ServiceIssue>> GetByCompanyAsync(int companyId);
        Task<List<ServiceIssue>> GetByAppointmentAsync(int appointmentId);
    }

    public class ServiceIssueRepository : GenericRepository<ServiceIssue>, IServiceIssueRepository
    {
        private readonly DbContextClass _context;

        public ServiceIssueRepository(DbContextClass context) : base(context)
        {
            _context = context;
        }

        public async Task<ServiceIssue?> GetByIdAsync(int id)
        {
            return await _context.ServiceIssues
                .Include(x => x.Appointment)
                .Include(x => x.Customer)
                .Include(x => x.CustomerAddress)
                .Include(x => x.Professional)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<ServiceIssue>> GetByCompanyAsync(int companyId)
        {
            return await _context.ServiceIssues
                .Include(x => x.Appointment)
                .Include(x => x.Customer)
                .Include(x => x.CustomerAddress)
                .Include(x => x.Professional)
                .Where(x => x.CompanyId == companyId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<ServiceIssue>> GetByAppointmentAsync(int appointmentId)
        {
            return await _context.ServiceIssues
                .Include(x => x.Customer)
                .Include(x => x.CustomerAddress)
                .Include(x => x.Professional)
                .Where(x => x.AppointmentId == appointmentId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }
    }
}
