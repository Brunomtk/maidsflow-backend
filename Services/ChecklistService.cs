using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Checklist;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services
{
    public interface IChecklistService
    {
        Task<Checklist?> CreateAsync(CreateChecklistDTO dto);
        Task<Checklist?> GetByIdAsync(int id);
        Task<Infrastructure.ServiceExtension.PagedResult<Checklist>> GetPagedAsync(ChecklistFiltersDTO filters);
        Task<bool> UpdateItemAsync(UpdateChecklistItemDTO dto);
        Task<bool> AddPhotosAsync(AddChecklistItemPhotosDTO dto);
        Task<bool> RemovePhotoAsync(int photoId);
        Task<bool> ConcludeAsync(int checklistId);
        Task<bool> UpdateMetaAsync(UpdateChecklistMetaDTO dto);
        Task<bool> DeleteAsync(int checklistId);
    }

    public class ChecklistService : IChecklistService
    {
        private readonly IUnitOfWork _uow;
        public ChecklistService(IUnitOfWork uow) => _uow = uow;

        public async Task<Checklist?> CreateAsync(CreateChecklistDTO dto)
        {
            var ck = new Checklist
            {
                CustomerId = dto.CustomerId,
                ObservacoesGerais = dto.ObservacoesGerais,
                AppointmentId = dto.AppointmentId,
                ProfessionalId = dto.ProfessionalId,
                Status = ChecklistStatus.EmAndamento
            };
            await _uow.Checklists.Add(ck);
            var saved = await _uow.SaveAsync() > 0;
            return saved ? ck : null;
        }

        public Task<Checklist?> GetByIdAsync(int id) => _uow.Checklists.GetByIdWithItemsAsync(id);

        public Task<Infrastructure.ServiceExtension.PagedResult<Checklist>> GetPagedAsync(ChecklistFiltersDTO filters) =>
            _uow.Checklists.GetPagedAsync(filters);

        public async Task<bool> UpdateItemAsync(UpdateChecklistItemDTO dto)
        {
            var item = await _uow.ChecklistItems.GetWithPhotosAsync(dto.ItemId);
            if (item == null) return false;

            item.Status = dto.Status;
            item.Observacoes = dto.Observacoes;
            _uow.ChecklistItems.Update(item);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> AddPhotosAsync(AddChecklistItemPhotosDTO dto)
        {
            var item = await _uow.ChecklistItems.GetWithPhotosAsync(dto.ItemId);
            if (item == null) return false;

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
            _uow.ChecklistItemPhotos.Delete(photo);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> ConcludeAsync(int checklistId)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(checklistId);
            if (ck == null) return false;
            ck.Status = ChecklistStatus.Concluido;
            _uow.Checklists.Update(ck);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> UpdateMetaAsync(UpdateChecklistMetaDTO dto)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(dto.ChecklistId);
            if (ck == null) return false;

            if (dto.AppointmentId.HasValue) ck.AppointmentId = dto.AppointmentId;
            if (dto.ProfessionalId.HasValue) ck.ProfessionalId = dto.ProfessionalId;

            _uow.Checklists.Update(ck);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int checklistId)
        {
            var ck = await _uow.Checklists.GetByIdWithItemsAsync(checklistId);
            if (ck == null) return false;
            _uow.Checklists.Delete(ck);
            return await _uow.SaveAsync() > 0;
        }
    }
}
