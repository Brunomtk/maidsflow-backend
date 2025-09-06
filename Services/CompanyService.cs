using Core.DTO;
using Core.DTO.Company;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompanyService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> CreateCompany(Company company)
        {
            if (company == null) return false;

            await _unitOfWork.Companies.Add(company);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<IEnumerable<Company>> GetAllCompanies()
        {
            return await _unitOfWork.Companies.GetAll();
        }

        public async Task<Company?> GetCompanyById(int companyId)
        {
            if (companyId <= 0) return null;
            return await _unitOfWork.Companies.GetByIdAsync(companyId);
        }

        public async Task<Company?> GetCompanyByCnpj(string cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return null;
            return await _unitOfWork.Companies.GetByCnpj(cnpj);
        }

        public async Task<PagedResult<Company>> GetCompaniesPagedFilteredAsync(CompanyFiltersDTO filters)
        {
            return await _unitOfWork.Companies.GetCompaniesPagedFilteredAsync(filters);
        }

        public async Task<int?> GetPlanIdByCompanyId(int companyId)
        {
            return await _unitOfWork.Companies.GetPlanIdByCompanyId(companyId);
        }

        public async Task<bool> UpdateCompany(CreateCompanyRequest request, int companyId)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null) return false;

            company.Name = request.Name;
            company.Cnpj = request.Cnpj;
            company.Responsible = request.Responsible;
            company.Email = request.Email;
            company.Phone = request.Phone;
            company.PlanId = request.PlanId;
            company.Status = request.Status;

            _unitOfWork.Companies.Update(company);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<bool> DeleteCompany(int companyId)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null) return false;

            _unitOfWork.Companies.Delete(company);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }
    }

    public interface ICompanyService
    {
        Task<bool> CreateCompany(Company company);
        Task<IEnumerable<Company>> GetAllCompanies();
        Task<Company?> GetCompanyById(int companyId);
        Task<Company?> GetCompanyByCnpj(string cnpj);
        Task<PagedResult<Company>> GetCompaniesPagedFilteredAsync(CompanyFiltersDTO filters);
        Task<int?> GetPlanIdByCompanyId(int companyId);
        Task<bool> UpdateCompany(CreateCompanyRequest request, int companyId);
        Task<bool> DeleteCompany(int companyId);
    }
}
