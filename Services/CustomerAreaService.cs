using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Checklist;
using Core.Models;
using Infrastructure.Repositories;

namespace Services
{
    public interface ICustomerAreaService
    {
        Task<CustomerArea?> CreateAsync(CreateCustomerAreaDTO dto);
        Task<bool> UpdateAsync(UpdateCustomerAreaDTO dto);
        Task<bool> SoftDeleteAsync(int id);
        IQueryable<CustomerArea> QueryByCustomer(int customerId, bool onlyActive);
    }

    public class CustomerAreaService : ICustomerAreaService
    {
        private readonly IUnitOfWork _uow;
        public CustomerAreaService(IUnitOfWork uow) => _uow = uow;

        public async Task<CustomerArea?> CreateAsync(CreateCustomerAreaDTO dto)
        {
            // Avoid duplicate active area names per customer
            if (await _uow.CustomerAreas.ExistsActiveByNameAsync(dto.CustomerId, dto.Name))
                return null;

            var area = new CustomerArea
            {
                CustomerId = dto.CustomerId,
                Name = dto.Name,
                Active = true
            };

            await _uow.CustomerAreas.Add(area);
            var saved = await _uow.SaveAsync() > 0;
            return saved ? area : null;
        }

        public async Task<bool> UpdateAsync(UpdateCustomerAreaDTO dto)
        {
            var area = await _uow.CustomerAreas.GetByIdAsync(dto.Id);
            if (area == null) return false;

            if (dto.Name != null)
            {
                // enforce uniqueness if changing name
                if (!string.Equals(area.Name, dto.Name, System.StringComparison.OrdinalIgnoreCase) &&
                    await _uow.CustomerAreas.ExistsActiveByNameAsync(area.CustomerId, dto.Name, excludeId: dto.Id))
                    return false;

                area.Name = dto.Name;
            }

            if (dto.Active.HasValue) area.Active = dto.Active.Value;

            _uow.CustomerAreas.Update(area);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var area = await _uow.CustomerAreas.GetByIdAsync(id);
            if (area == null) return false;

            area.Active = false;
            _uow.CustomerAreas.Update(area);
            return await _uow.SaveAsync() > 0;
        }

        public IQueryable<CustomerArea> QueryByCustomer(int customerId, bool onlyActive) =>
            _uow.CustomerAreas.QueryByCustomer(customerId, onlyActive);
    }
}
