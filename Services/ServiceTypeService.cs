using Core.DTO.ServiceTypes;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IServiceTypeService
    {
        Task<List<ServiceTypeDTO>> GetByCompanyAsync(int companyId, bool includeInactive = false);
        Task<ServiceTypeDTO?> GetByIdAsync(int id);
        Task<ServiceTypeDTO> CreateAsync(CreateServiceTypeDTO dto);
        Task<ServiceTypeDTO> UpdateAsync(int id, UpdateServiceTypeDTO dto);
        Task<bool> DeleteAsync(int id);
    }

    public class ServiceTypeService : IServiceTypeService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ServiceTypeService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<ServiceTypeDTO>> GetByCompanyAsync(int companyId, bool includeInactive = false)
        {
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(companyId);

            var list = await _uow.ServiceTypes.GetByCompanyAsync(companyId, includeInactive);
            var outList = new List<ServiceTypeDTO>();
            foreach (var st in list)
                outList.Add(ToDto(st));
            return outList;
        }

        public async Task<ServiceTypeDTO?> GetByIdAsync(int id)
        {
            var st = await _uow.ServiceTypes.GetByIdWithCompanyAsync(id);
            if (st == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(st.CompanyId);

            return ToDto(st);
        }

        public async Task<ServiceTypeDTO> CreateAsync(CreateServiceTypeDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new BadRequestException("Name é obrigatório.");

            // Company scope
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                dto.CompanyId = scopedCompanyId.Value;
            }

            if (!dto.CompanyId.HasValue)
                throw new BadRequestException("CompanyId é obrigatório.");

            var companyId = dto.CompanyId.Value;

            // Evita duplicados por company
            if (await _uow.ServiceTypes.ExistsByNameAsync(companyId, dto.Name))
                throw new BadRequestException("Já existe um ServiceType com este nome para esta company.");

            var st = new ServiceType
            {
                CompanyId = companyId,
                Name = dto.Name.Trim(),
                IsActive = dto.IsActive ?? true,
                Description = dto.Description
            };

            await _uow.ServiceTypes.Add(st);
            await _uow.SaveAsync();

            return ToDto(st);
        }

        public async Task<ServiceTypeDTO> UpdateAsync(int id, UpdateServiceTypeDTO dto)
        {
            var st = await _uow.ServiceTypes.GetById(id);
            if (st == null) throw new NotFoundException("ServiceType não encontrado.");

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(st.CompanyId);

            if (dto.Name != null)
            {
                var name = dto.Name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new BadRequestException("Name não pode ser vazio.");

                if (await _uow.ServiceTypes.ExistsByNameAsync(st.CompanyId, name, ignoreId: id))
                    throw new BadRequestException("Já existe um ServiceType com este nome para esta company.");

                st.Name = name;
            }

            if (dto.IsActive.HasValue) st.IsActive = dto.IsActive.Value;
            if (dto.Description != null) st.Description = dto.Description;

            _uow.ServiceTypes.Update(st);
            await _uow.SaveAsync();

            return ToDto(st);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var st = await _uow.ServiceTypes.GetById(id);
            if (st == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(st.CompanyId);

            // Soft delete: desativa
            st.IsActive = false;
            _uow.ServiceTypes.Update(st);
            await _uow.SaveAsync();

            return true;
        }

        private static ServiceTypeDTO ToDto(ServiceType st) => new ServiceTypeDTO
        {
            Id = st.Id,
            CompanyId = st.CompanyId,
            Name = st.Name,
            IsActive = st.IsActive,
            Description = st.Description
        };
    }
}
