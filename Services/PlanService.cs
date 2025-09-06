using Core.DTO;
using Core.Enums;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Core.Models;

namespace Services
{
    public class PlanService : IPlanService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PlanService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<Plan>> GetPlansPaged(FiltersDTO filtersDTO)
        {
            return await _unitOfWork.Plans.GetPlansPaged(filtersDTO);
        }

        public async Task<IEnumerable<Plan>> GetAllPlans()
        {
            return await _unitOfWork.Plans.GetAll();
        }

        public async Task<Plan?> GetPlanById(int id)
        {
            return await _unitOfWork.Plans.GetById(id);
        }

        public async Task<bool> CreatePlan(Plan plan)
        {
            await _unitOfWork.Plans.Add(plan);
            return _unitOfWork.Save() > 0;
        }

        public async Task<bool> UpdatePlan(Plan plan)
        {
            var existing = await _unitOfWork.Plans.GetById(plan.Id);
            if (existing == null) return false;

            existing.Name = plan.Name;
            existing.Price = plan.Price;
            existing.Features = plan.Features;
            existing.ProfessionalsLimit = plan.ProfessionalsLimit;
            existing.TeamsLimit = plan.TeamsLimit;
            existing.CustomersLimit = plan.CustomersLimit;
            existing.AppointmentsLimit = plan.AppointmentsLimit;
            existing.Duration = plan.Duration;
            existing.Status = plan.Status;

            _unitOfWork.Plans.Update(existing);
            return _unitOfWork.Save() > 0;
        }

        public async Task<bool> DeletePlan(int id)
        {
            var plan = await _unitOfWork.Plans.GetById(id);
            if (plan == null) return false;

            _unitOfWork.Plans.Delete(plan);
            return _unitOfWork.Save() > 0;
        }

        public async Task<bool> UpdateStatus(int id, StatusEnum status)
        {
            var plan = await _unitOfWork.Plans.GetById(id);
            if (plan == null) return false;

            plan.Status = status;
            _unitOfWork.Plans.Update(plan);
            return _unitOfWork.Save() > 0;
        }
    }

    public interface IPlanService
    {
        Task<PagedResult<Plan>> GetPlansPaged(FiltersDTO filtersDTO);
        Task<IEnumerable<Plan>> GetAllPlans();
        Task<Plan?> GetPlanById(int id);
        Task<bool> CreatePlan(Plan plan);
        Task<bool> UpdatePlan(Plan plan);
        Task<bool> DeletePlan(int id);
        Task<bool> UpdateStatus(int id, StatusEnum status);
    }
}
