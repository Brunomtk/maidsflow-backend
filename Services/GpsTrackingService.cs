using System;
using System.Threading.Tasks;
using Core.DTO.GpsTracking;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

namespace Services
{
    public interface IGpsTrackingService
    {
        Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters);
        Task<GpsTracking?> GetByIdAsync(int id);
        Task<GpsTracking> CreateAsync(CreateGpsTrackingDTO dto);
        Task<GpsTracking?> UpdateAsync(int id, UpdateGpsTrackingDTO dto);
        Task<bool> DeleteAsync(int id);
    }

    public class GpsTrackingService : IGpsTrackingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public GpsTrackingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters)
        {
            return await _unitOfWork.GpsTrackings.GetPagedAsync(filters);
        }

        public async Task<GpsTracking?> GetByIdAsync(int id)
        {
            return await _unitOfWork.GpsTrackings.GetByIdAsync(id);
        }

        public async Task<GpsTracking> CreateAsync(CreateGpsTrackingDTO dto)
        {
            var model = new GpsTracking
            {
                ProfessionalId = dto.ProfessionalId,
                ProfessionalName = dto.ProfessionalName,
                CompanyId = dto.CompanyId,
                CompanyName = dto.CompanyName,
                Vehicle = dto.Vehicle,
                Location = new Location
                {
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    Address = dto.Address,
                    Accuracy = dto.Accuracy
                },
                Speed = dto.Speed,
                Status = dto.Status,
                Battery = dto.Battery,
                Notes = dto.Notes,
                Timestamp = dto.Timestamp,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.GpsTrackings.Add(model);
            await _unitOfWork.SaveAsync();
            return model;
        }

        public async Task<GpsTracking?> UpdateAsync(int id, UpdateGpsTrackingDTO dto)
        {
            var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
            if (model == null) return null;

            if (dto.ProfessionalId.HasValue)
                model.ProfessionalId = dto.ProfessionalId.Value;
            if (!string.IsNullOrEmpty(dto.ProfessionalName))
                model.ProfessionalName = dto.ProfessionalName;
            if (dto.CompanyId.HasValue)
                model.CompanyId = dto.CompanyId.Value;
            if (!string.IsNullOrEmpty(dto.CompanyName))
                model.CompanyName = dto.CompanyName;
            if (!string.IsNullOrEmpty(dto.Vehicle))
                model.Vehicle = dto.Vehicle;

            if (dto.Latitude.HasValue)
                model.Location.Latitude = dto.Latitude.Value;
            if (dto.Longitude.HasValue)
                model.Location.Longitude = dto.Longitude.Value;
            if (!string.IsNullOrEmpty(dto.Address))
                model.Location.Address = dto.Address;
            if (dto.Accuracy.HasValue)
                model.Location.Accuracy = dto.Accuracy.Value;

            if (dto.Speed.HasValue)
                model.Speed = dto.Speed.Value;
            if (dto.Status.HasValue)
                model.Status = dto.Status.Value;
            if (dto.Battery.HasValue)
                model.Battery = dto.Battery.Value;
            if (!string.IsNullOrEmpty(dto.Notes))
                model.Notes = dto.Notes;
            if (dto.Timestamp.HasValue)
                model.Timestamp = dto.Timestamp.Value;

            model.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.GpsTrackings.Update(model);
            await _unitOfWork.SaveAsync();
            return model;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
            if (model == null) return false;

            _unitOfWork.GpsTrackings.Delete(model);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}