using Core.DTO.Company;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public CompanyService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<IEnumerable<Company>> GetAllCompanies()
        {
            if (_currentUser.IsAdmin)
                return await _unitOfWork.Companies.GetAll();

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                return Enumerable.Empty<Company>();

            var company = await _unitOfWork.Companies.GetByIdAsync(scopedCompanyId.Value);
            return company == null ? Enumerable.Empty<Company>() : new[] { company };
        }

        public async Task<Company?> GetCompanyById(int companyId)
        {
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(companyId);

            return await _unitOfWork.Companies.GetByIdAsync(companyId);
        }

        public async Task<Company?> GetCompanyByCnpj(string cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return null;

            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode buscar company por CNPJ.");

            return await _unitOfWork.Companies.GetByCnpj(cnpj);
        }

        public async Task<PagedResult<Company>> GetCompaniesPagedFilteredAsync(CompanyFiltersDTO filters)
        {
            if (_currentUser.IsAdmin)
                return await _unitOfWork.Companies.GetCompaniesPagedFilteredAsync(filters);

            // Company / Professional: retorna apenas a própria company como "paged"
            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
            {
                return new PagedResult<Company>
                {
                    CurrentPage = filters.Page,
                    PageSize = filters.PageSize,
                    PageCount = 0,
                    TotalItems = 0,
                    Results = new List<Company>()
                };
            }

            var company = await _unitOfWork.Companies.GetByIdAsync(scopedCompanyId.Value);
            if (company == null)
            {
                return new PagedResult<Company>
                {
                    CurrentPage = filters.Page,
                    PageSize = filters.PageSize,
                    PageCount = 0,
                    TotalItems = 0,
                    Results = new List<Company>()
                };
            }

            return new PagedResult<Company>
            {
                CurrentPage = 1,
                PageSize = filters.PageSize,
                PageCount = 1,
                TotalItems = 1,
                Results = new List<Company> { company }
            };
        }

        public async Task<int?> GetPlanIdByCompanyId(int companyId)
        {
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(companyId);

            return await _unitOfWork.Companies.GetPlanIdByCompanyId(companyId);
        }

        public async Task<bool> CreateCompany(Company company)
        {
            // Allow creating a company only in these cases:
            // 1) Admin user.
            // 2) Anonymous signup (no NameIdentifier/Role claims -> UserId == 0).
            // 3) Company role user that is not yet scoped to any Company (CompanyId == null).
            //    This covers flows where the user is authenticated but still completing onboarding.
            // Any other authenticated non-admin (company already scoped / professional) is forbidden.
            var isAnonymous = _currentUser.UserId == 0;
            var isCompanyWithoutScope = _currentUser.IsCompany && !_currentUser.CompanyId.HasValue;

            if (!_currentUser.IsAdmin && !isAnonymous && !isCompanyWithoutScope)
                throw new ForbiddenException("Somente admin ou signup podem criar companies.");

            await _unitOfWork.Companies.Add(company);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<bool> UpdateCompany(CreateCompanyRequest request, int companyId)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para atualizar company.");

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(companyId);

            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null) return false;

            company.Name = request.Name;
            company.Cnpj = request.Cnpj;
            company.Responsible = request.Responsible;
            company.Email = request.Email;
            company.Phone = request.Phone;
            if (request.PlanId.HasValue) company.PlanId = request.PlanId;
            company.Status = request.Status;

            _unitOfWork.Companies.Update(company);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<bool> DeleteCompany(int companyId)
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode deletar companies.");

            var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
            if (company == null) return false;

            _unitOfWork.Companies.Delete(company);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }
    }

    public interface ICompanyService
    {
        Task<IEnumerable<Company>> GetAllCompanies();
        Task<Company?> GetCompanyById(int companyId);
        Task<Company?> GetCompanyByCnpj(string cnpj);
        Task<PagedResult<Company>> GetCompaniesPagedFilteredAsync(CompanyFiltersDTO filters);
        Task<int?> GetPlanIdByCompanyId(int companyId);
        Task<bool> CreateCompany(Company company);
        Task<bool> UpdateCompany(CreateCompanyRequest request, int companyId);
        Task<bool> DeleteCompany(int companyId);
    }
}
