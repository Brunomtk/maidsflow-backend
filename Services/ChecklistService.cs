using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Checklist;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Services.Storage;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public interface IChecklistService
    {
        Task<Checklist?> CreateAsync(CreateChecklistDTO dto);
        Task<Checklist?> GetByIdAsync(int id);
        Task<Infrastructure.ServiceExtension.PagedResult<Checklist>> GetPagedAsync(ChecklistFiltersDTO filters);
        Task<bool> UpdateItemAsync(UpdateChecklistItemDTO dto);
        Task<bool> AddPhotosAsync(AddChecklistItemPhotosDTO dto);
        Task<int> AddItemAsync(CreateChecklistItemDTO dto);
        Task<int> EnsureItemsFromAreasAsync(int checklistId);
        Task<bool> RemovePhotoAsync(int photoId);
        Task<bool> ConcludeAsync(int checklistId);
        Task<bool> UpdateMetaAsync(UpdateChecklistMetaDTO dto);
        Task<bool> DeleteAsync(int checklistId);
    }

    public class ChecklistService : IChecklistService
    {
        private readonly IUnitOfWork _uow;
        private readonly IS3StorageService _s3;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ChecklistService(IUnitOfWork uow, IS3StorageService s3, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _s3 = s3;
            _currentUser = currentUser;
            _scope = scope;
        }

        private async Task EnsureChecklistScopedAsync(Checklist ck)
        {
            await _scope.EnsureCompanyAccessAsync(ck.CompanyId);
            if (_currentUser.IsProfessional)
            {
                var profId = await _scope.GetScopedProfessionalIdAsync();
                if (!profId.HasValue)
                    throw new ForbiddenException("Escopo de profissional inválido.");

                // Professional only sees checklists assigned to itself
                if (!ck.ProfessionalId.HasValue || ck.ProfessionalId.Value != profId.Value)
                    throw new ForbiddenException("Você não tem permissão para acessar este checklist.");
            }
        }

        public async Task<Checklist?> CreateAsync(CreateChecklistDTO dto)
        {
            // Professionals can create checklists only for themselves (within their company)
            if (_currentUser.IsProfessional)
            {
                var profId = await _scope.GetScopedProfessionalIdAsync();
                var companyId = await _scope.GetScopedCompanyIdAsync();

                if (!profId.HasValue || !companyId.HasValue)
                    throw new ForbiddenException("Escopo inválido.");

                dto.CompanyId = companyId.Value;
                dto.ProfessionalId = profId.Value;
            }
            else if (_currentUser.IsCompany)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (!companyId.HasValue) throw new ForbiddenException("Escopo de company inválido.");
                dto.CompanyId = companyId.Value;

                // If company sets a ProfessionalId, ensure it belongs to the same company
                if (dto.ProfessionalId.HasValue)
                    await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId.Value);
            }
            // admin: no changes

            // Ensure customer belongs to company scope
            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);

            // Validate optional FKs
            int? appointmentId = null;
            if (dto.AppointmentId.HasValue)
            {
                await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId.Value);
                var appointment = await _uow.Appointments.GetById(dto.AppointmentId.Value);
                if (appointment != null) appointmentId = dto.AppointmentId.Value;
            }

            int? professionalId = null;
            if (dto.ProfessionalId.HasValue)
            {
                await _scope.EnsureProfessionalAccessAsync(dto.ProfessionalId.Value);
                var professional = await _uow.Professionals.GetByIdAsync(dto.ProfessionalId.Value);
                if (professional != null) professionalId = dto.ProfessionalId.Value;
            }

            var ck = new Checklist
            {
                CustomerId = dto.CustomerId,
                CompanyId = dto.CompanyId,
                ObservacoesGerais = dto.ObservacoesGerais,
                AppointmentId = appointmentId,
                ProfessionalId = professionalId,
                Status = ChecklistStatus.EmAndamento
            };

            await _uow.Checklists.Add(ck);
            var saved = await _uow.SaveAsync() > 0;
            return saved ? ck : null;
        }

        public async Task<Checklist?> GetByIdAsync(int id)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(id);
            if (ck == null) return null;
            await EnsureChecklistScopedAsync(ck);
            return ck;
        }

        public async Task<Infrastructure.ServiceExtension.PagedResult<Checklist>> GetPagedAsync(ChecklistFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (!companyId.HasValue) throw new ForbiddenException("Escopo de company inválido.");

                filters.CompanyId = companyId.Value;

                if (_currentUser.IsProfessional)
                {
                    var profId = await _scope.GetScopedProfessionalIdAsync();
                    filters.ProfessionalId = profId;
                }
            }

            // If filters include appointment/professional/customer, validate scope
            if (filters.CustomerId.HasValue)
                await _scope.EnsureCustomerInCompanyAsync(filters.CustomerId.Value);
            if (filters.ProfessionalId.HasValue)
                await _scope.EnsureProfessionalAccessAsync(filters.ProfessionalId.Value);
            if (filters.AppointmentId.HasValue)
                await _scope.EnsureAppointmentAccessAsync(filters.AppointmentId.Value);

            return await _uow.Checklists.GetPagedAsync(filters);
        }

        public async Task<bool> UpdateItemAsync(UpdateChecklistItemDTO dto)
        {
            var item = await _uow.ChecklistItems.GetWithPhotosAsync(dto.ItemId);
            if (item == null) return false;

            // Load checklist to check scope
            // IChecklistRepository herda de IGenericRepository e expõe GetById(int) (sem sufixo Async)
            var ck = await _uow.Checklists.GetById(item.ChecklistId);
            if (ck == null) return false;
            await EnsureChecklistScopedAsync(ck);

            item.Status = dto.Status;
            item.Observacoes = dto.Observacoes;
            _uow.ChecklistItems.Update(item);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> AddPhotosAsync(AddChecklistItemPhotosDTO dto)
        {
            var item = await _uow.ChecklistItems.GetWithPhotosAsync(dto.ItemId);
            if (item == null) return false;

            var ck = await _uow.Checklists.GetById(item.ChecklistId);
            if (ck == null) return false;
            await EnsureChecklistScopedAsync(ck);

            var photos = new List<ChecklistItemPhoto>();
            for (int i = 0; i < dto.Urls.Count; i++)
            {
                var url = dto.Urls[i];
                string? desc = (dto.Descriptions != null && i < dto.Descriptions.Count) ? dto.Descriptions[i] : null;
                photos.Add(new ChecklistItemPhoto { ChecklistItemId = item.Id, Url = url, Descricao = desc });
            }

            foreach (var ph in photos) await _uow.ChecklistItemPhotos.Add(ph);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> RemovePhotoAsync(int photoId)
        {
            var photo = await _uow.ChecklistItemPhotos.GetByIdAsync(photoId);
            if (photo == null) return false;

            // IChecklistItemRepository herda de IGenericRepository e expõe GetById(int)
            var item = await _uow.ChecklistItems.GetById(photo.ChecklistItemId);
            if (item == null) return false;

            var ck = await _uow.Checklists.GetById(item.ChecklistId);
            if (ck == null) return false;
            await EnsureChecklistScopedAsync(ck);

            if (!string.IsNullOrWhiteSpace(photo.Url) && _s3.TryGetKeyFromStoredValue(photo.Url, out var key))
            {
                await _s3.DeleteIfExistsAsync(key);
            }

            _uow.ChecklistItemPhotos.Delete(photo);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> ConcludeAsync(int checklistId)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(checklistId);
            if (ck == null) return false;
            await EnsureChecklistScopedAsync(ck);

            ck.Status = ChecklistStatus.Concluido;
            _uow.Checklists.Update(ck);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> UpdateMetaAsync(UpdateChecklistMetaDTO dto)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(dto.ChecklistId);
            if (ck == null) return false;
            await EnsureChecklistScopedAsync(ck);

            if (dto.AppointmentId.HasValue)
            {
                await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId.Value);
                ck.AppointmentId = dto.AppointmentId;
            }

            if (dto.ProfessionalId.HasValue)
            {
                await _scope.EnsureProfessionalAccessAsync(dto.ProfessionalId.Value);
                ck.ProfessionalId = dto.ProfessionalId;
            }

            _uow.Checklists.Update(ck);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<int> AddItemAsync(CreateChecklistItemDTO dto)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(dto.ChecklistId);
            if (ck == null) return 0;
            await EnsureChecklistScopedAsync(ck);

            var area = await _uow.CustomerAreas.GetByIdAsync(dto.CustomerAreaId);
            if (area == null || !area.Active || area.CustomerId != ck.CustomerId) return 0;

            var existing = ck.Items.FirstOrDefault(i => i.CustomerAreaId == area.Id);
            if (existing != null) return existing.Id;

            var item = new ChecklistItem
            {
                ChecklistId = ck.Id,
                CustomerAreaId = area.Id,
                Observacoes = dto.Observacoes
            };

            await _uow.ChecklistItems.Add(item);
            await _uow.SaveAsync();
            return item.Id;
        }

        public async Task<int> EnsureItemsFromAreasAsync(int checklistId)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(checklistId);
            if (ck == null) return 0;
            await EnsureChecklistScopedAsync(ck);

            var existingAreaIds = ck.Items.Select(i => i.CustomerAreaId).ToHashSet();
            var areas = await _uow.CustomerAreas.QueryByCustomer(ck.CustomerId, onlyActive: true).ToListAsync();

            int created = 0;
            foreach (var area in areas)
            {
                if (!existingAreaIds.Contains(area.Id))
                {
                    await _uow.ChecklistItems.Add(new ChecklistItem
                    {
                        ChecklistId = ck.Id,
                        CustomerAreaId = area.Id
                    });
                    created++;
                }
            }

            if (created > 0) await _uow.SaveAsync();
            return created;
        }

        public async Task<bool> DeleteAsync(int checklistId)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(checklistId);
            if (ck == null) return false;
            await EnsureChecklistScopedAsync(ck);

            _uow.Checklists.Delete(ck);
            return await _uow.SaveAsync() > 0;
        }
    }
}
