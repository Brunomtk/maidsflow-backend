using System;
using System.Threading.Tasks;
using Core.DTO.GpsTracking;
using Core.Enums.GpsTracking;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services.Security;
using Core.Exceptions;

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
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public GpsTrackingService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<GpsTracking>> GetPagedAsync(GpsTrackingFiltersDTO filters)
{
    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) filters.CompanyId = companyId.Value;

        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (pid.HasValue) filters.ProfessionalId = pid.Value;
        }
    }

    return await _unitOfWork.GpsTrackings.GetPagedAsync(filters);
}


        public async Task<GpsTracking?> GetByIdAsync(int id)
{
    var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
    if (model == null) return null;

    if (!_currentUser.IsAdmin)
    {
        await _scope.EnsureCompanyAccessAsync(model.CompanyId);

        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (!pid.HasValue || pid.Value != model.ProfessionalId)
                throw new ForbiddenException("Você não tem permissão para acessar este GPS Tracking.");
        }
    }

    return model;
}


        public async Task<GpsTracking> CreateAsync(CreateGpsTrackingDTO dto)
{
    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) dto.CompanyId = companyId.Value;

        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (!pid.HasValue) throw new ForbiddenException("Escopo de profissional inválido.");
            dto.ProfessionalId = pid.Value;
        }

        // garante que profissional pertence à company
        await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId);
    }

            var model = new GpsTracking
            {
                ProfessionalId = dto.ProfessionalId,
                ProfessionalName = dto.ProfessionalName,
                CompanyId = dto.CompanyId,
                CompanyName = dto.CompanyName,
                TeamId = dto.TeamId,
                Vehicle = dto.Vehicle ?? string.Empty,
                Location = new Location
                {
                    Latitude = dto.Latitude ?? 0,
                    Longitude = dto.Longitude ?? 0,
                    Address = dto.Address ?? string.Empty,
                    Accuracy = dto.Accuracy.HasValue ? dto.Accuracy.Value : 0
                },
                Speed = dto.Speed ?? 0,
                Status = dto.Status ?? GpsTrackingStatus.Active,
                Battery = dto.Battery ?? 0,
                Notes = dto.Notes,
                Timestamp = dto.Timestamp ?? DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            await _unitOfWork.GpsTrackings.Add(model);
            await _unitOfWork.SaveAsync();
            return model;
        }

        public async Task<GpsTracking?> UpdateAsync(int id, UpdateGpsTrackingDTO dto)
        {
            var model = await _unitOfWork.GpsTrackings.GetByIdAsync(id);
            if (model == null) return null;

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(model.CompanyId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != model.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para editar este GPS Tracking.");
                }
            }

            // ProfessionalId/CompanyId não alteráveis aqui (só admin)
            if (_currentUser.IsAdmin && dto.ProfessionalId.HasValue)
                model.ProfessionalId = dto.ProfessionalId.Value;

            if (!string.IsNullOrWhiteSpace(dto.ProfessionalName))
                model.ProfessionalName = dto.ProfessionalName;

            if (_currentUser.IsAdmin && dto.CompanyId.HasValue)
                model.CompanyId = dto.CompanyId.Value;

            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
                model.CompanyName = dto.CompanyName;

            if (dto.TeamId.HasValue)
                model.TeamId = dto.TeamId;

            if (!string.IsNullOrWhiteSpace(dto.Vehicle))
                model.Vehicle = dto.Vehicle;

            if (dto.Latitude.HasValue)
                model.Location.Latitude = dto.Latitude.Value;
            if (dto.Longitude.HasValue)
                model.Location.Longitude = dto.Longitude.Value;
            if (!string.IsNullOrWhiteSpace(dto.Address))
                model.Location.Address = dto.Address;
            if (dto.Accuracy.HasValue)
                model.Location.Accuracy = dto.Accuracy.Value;

            if (dto.Speed.HasValue)
                model.Speed = dto.Speed.Value;

            if (dto.Status.HasValue)
                model.Status = dto.Status.Value;

            if (dto.Battery.HasValue)
                model.Battery = dto.Battery.Value;

            if (dto.Notes != null)
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

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(model.CompanyId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != model.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para excluir este GPS Tracking.");
                }
            }

            _unitOfWork.GpsTrackings.Delete(model);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}
