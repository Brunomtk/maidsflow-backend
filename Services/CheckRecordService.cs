using System;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.CheckRecord;
using Core.Enums.CheckRecord;
using Core.Enums.GpsTracking;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public interface ICheckRecordService
    {
        Task<CheckRecord?> GetByIdAsync(int id);
        Task<PagedResult<CheckRecord>> GetPagedAsync(CheckRecordFiltersDTO filters);
        Task<CheckRecord> CreateAsync(CreateCheckRecordDTO dto);
        Task<CheckRecord?> UpdateAsync(int id, UpdateCheckRecordDTO dto);
        Task<bool> DeleteAsync(int id);
        Task<CheckRecord> PerformCheckInAsync(CreateCheckRecordDTO dto);
        Task<CheckRecord?> PerformCheckOutAsync(int id);
    }

    public class CheckRecordService : ICheckRecordService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public CheckRecordService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<CheckRecord?> GetByIdAsync(int id)
        {
            var record = await _unitOfWork.CheckRecords.GetByIdAsync(id);
            if (record == null) return null;

            await _scope.EnsureCompanyAccessAsync(record.CompanyId);

            if (_currentUser.IsProfessional)
            {
                var profId = await _scope.GetScopedProfessionalIdAsync();
                if (!profId.HasValue || record.ProfessionalId != profId.Value)
                    throw new ForbiddenException("Você não tem permissão para acessar este check record.");
            }

            return record;
        }

        public async Task<PagedResult<CheckRecord>> GetPagedAsync(CheckRecordFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                filters.CompanyId = companyId;

                if (_currentUser.IsProfessional)
                {
                    var profId = await _scope.GetScopedProfessionalIdAsync();
                    filters.ProfessionalId = profId;
                }
            }

            return await _unitOfWork.CheckRecords.GetPagedAsync(filters);
        }

        public async Task<CheckRecord> CreateAsync(CreateCheckRecordDTO dto)
        {
            // Only professionals (self) or company/admin can create.
            if (_currentUser.IsProfessional)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                var scopedProfId = await _scope.GetScopedProfessionalIdAsync();
                if (!scopedCompanyId.HasValue || !scopedProfId.HasValue)
                    throw new ForbiddenException("Escopo inválido.");

                dto.CompanyId = scopedCompanyId.Value;
                dto.ProfessionalId = scopedProfId.Value;
            }
            else if (_currentUser.IsCompany)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo inválido.");

                dto.CompanyId = scopedCompanyId.Value;
                // CreateCheckRecordDTO.ProfessionalId é int (obrigatório). Validar escopo.
                if (dto.ProfessionalId > 0)
                    await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId);
            }
            // admin leaves as-is.

            // Validate references when present
            // Appointment é obrigatório. Usamos ele como fonte de verdade para Customer/Address.
            await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId);

            var appointment = await _unitOfWork.Appointments.GetByIdWithDetailsAsync(dto.AppointmentId);
            if (appointment == null)
                throw new NotFoundException("Appointment não encontrado.");

            if (appointment.CustomerId.HasValue)
                dto.CustomerId = appointment.CustomerId.Value;

            // CustomerId é obrigatório no fluxo e precisa estar dentro da company
            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);

            if (dto.TeamId.HasValue) await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);

            dto.Address = FormatAppointmentAddress(appointment);


            var now = DateTime.UtcNow;
            var record = new CheckRecord
            {
                ProfessionalId = dto.ProfessionalId,
                ProfessionalName = dto.ProfessionalName,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                AppointmentId = dto.AppointmentId,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                TeamId = dto.TeamId,
                TeamName = dto.TeamName,
                ServiceType = dto.ServiceType,
                Notes = dto.Notes,
                Status = CheckRecordStatus.Pending,
                CreatedDate = now,
                UpdatedDate = now
            };

            await _unitOfWork.CheckRecords.Add(record);
            await _unitOfWork.SaveAsync();
            return record;
        }

        public async Task<CheckRecord?> UpdateAsync(int id, UpdateCheckRecordDTO dto)
        {
            // Professionals can only update their own record.
            var record = await _unitOfWork.CheckRecords.GetByIdAsync(id);
            if (record == null) return null;

            await _scope.EnsureCompanyAccessAsync(record.CompanyId);

            if (_currentUser.IsProfessional)
            {
                var profId = await _scope.GetScopedProfessionalIdAsync();
                if (!profId.HasValue || record.ProfessionalId != profId.Value)
                    throw new ForbiddenException("Você não tem permissão para editar este check record.");

                // Lock scope
                dto.CompanyId = record.CompanyId;
                dto.ProfessionalId = record.ProfessionalId;
            }
            else if (_currentUser.IsCompany)
            {
                // Company cannot move to another company
                dto.CompanyId = record.CompanyId;
                if (dto.ProfessionalId.HasValue)
                    await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId.Value);
            }

            // Validate FK changes
            if (dto.CustomerId.HasValue) await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId.Value);
            if (dto.TeamId.HasValue) await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);
            if (dto.AppointmentId.HasValue) await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId.Value);

            record.ProfessionalId = dto.ProfessionalId ?? record.ProfessionalId;
            record.ProfessionalName = dto.ProfessionalName ?? record.ProfessionalName;
            record.CompanyId = dto.CompanyId ?? record.CompanyId;
            record.CustomerId = dto.CustomerId ?? record.CustomerId;
            record.CustomerName = dto.CustomerName ?? record.CustomerName;
            record.AppointmentId = dto.AppointmentId ?? record.AppointmentId;
            record.Address = dto.Address ?? record.Address;
            record.Latitude = dto.Latitude ?? record.Latitude;
            record.Longitude = dto.Longitude ?? record.Longitude;
            record.TeamId = dto.TeamId ?? record.TeamId;
            record.TeamName = dto.TeamName ?? record.TeamName;
            record.CheckInTime = dto.CheckInTime ?? record.CheckInTime;
            record.CheckOutTime = dto.CheckOutTime ?? record.CheckOutTime;
            record.ServiceType = dto.ServiceType ?? record.ServiceType;
            record.Notes = dto.Notes ?? record.Notes;

            if (!string.IsNullOrEmpty(dto.Status) &&
                Enum.TryParse<CheckRecordStatus>(dto.Status, true, out var statusEnum))
            {
                record.Status = statusEnum;
            }

            record.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.CheckRecords.Update(record);
            await _unitOfWork.SaveAsync();

            return record;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Você não tem permissão para remover check records.");

            var record = await _unitOfWork.CheckRecords.GetByIdAsync(id);
            if (record == null) return false;

            await _scope.EnsureCompanyAccessAsync(record.CompanyId);

            _unitOfWork.CheckRecords.Delete(record);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<CheckRecord> PerformCheckInAsync(CreateCheckRecordDTO dto)
        {
            // Same scope rules as create
            if (_currentUser.IsProfessional)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                var scopedProfId = await _scope.GetScopedProfessionalIdAsync();
                if (!scopedCompanyId.HasValue || !scopedProfId.HasValue)
                    throw new ForbiddenException("Escopo inválido.");

                dto.CompanyId = scopedCompanyId.Value;
                dto.ProfessionalId = scopedProfId.Value;
            }
            else if (_currentUser.IsCompany)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo inválido.");

                dto.CompanyId = scopedCompanyId.Value;
                // CreateCheckRecordDTO.ProfessionalId é int (obrigatório). Validar escopo.
                if (dto.ProfessionalId > 0)
                    await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId);
            }

            // Appointment é obrigatório. Usamos ele como fonte de verdade para Customer/Address.
            await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId);

            var appointment = await _unitOfWork.Appointments.GetByIdWithDetailsAsync(dto.AppointmentId);
            if (appointment == null)
                throw new NotFoundException("Appointment não encontrado.");

            if (appointment.CustomerId.HasValue)
                dto.CustomerId = appointment.CustomerId.Value;

            // CustomerId é obrigatório no fluxo e precisa estar dentro da company
            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);

            if (dto.TeamId.HasValue) await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);

            dto.Address = FormatAppointmentAddress(appointment);


            var now = DateTime.UtcNow;

            var record = new CheckRecord
            {
                ProfessionalId = dto.ProfessionalId,
                ProfessionalName = dto.ProfessionalName,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                AppointmentId = dto.AppointmentId,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                TeamId = dto.TeamId,
                TeamName = dto.TeamName,
                ServiceType = dto.ServiceType,
                Notes = dto.Notes,
                Status = CheckRecordStatus.CheckedIn,
                CheckInTime = now,
                CreatedDate = now,
                UpdatedDate = now
            };

            await _unitOfWork.CheckRecords.Add(record);
            await _unitOfWork.SaveAsync();

            // Cria um ponto de rota baseado no check-in (para relatório do dia/semana/mês)
            await CreateGpsPointFromCheckInAsync(record);

            return record;
        }

        public async Task<CheckRecord?> PerformCheckOutAsync(int id)
        {
            var record = await _unitOfWork.CheckRecords.GetByIdAsync(id);
            if (record == null || record.Status != CheckRecordStatus.CheckedIn)
                return null;

            await _scope.EnsureCompanyAccessAsync(record.CompanyId);

            if (_currentUser.IsProfessional)
            {
                var profId = await _scope.GetScopedProfessionalIdAsync();
                if (!profId.HasValue || record.ProfessionalId != profId.Value)
                    throw new ForbiddenException("Você não tem permissão para fazer checkout deste check record.");
            }

            record.CheckOutTime = DateTime.UtcNow;
            record.Status = CheckRecordStatus.CheckedOut;
            record.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.CheckRecords.Update(record);
            await _unitOfWork.SaveAsync();

            return record;
        }

        private static string FormatAppointmentAddress(Appointment appointment)
        {
            if (appointment.CustomerAddress != null)
            {
                var a = appointment.CustomerAddress;

                var line1 = (a.AddressLine1 ?? string.Empty).Trim();
                var line2 = (a.AddressLine2 ?? string.Empty).Trim();
                var city = (a.City ?? string.Empty).Trim();
                var state = (a.State ?? string.Empty).Trim();
                var zip = (a.ZipCode ?? string.Empty).Trim();

                var firstPart = string.Join(", ", new[] { line1, line2 }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var secondPart = string.Join(", ", new[] { city, state }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var thirdPart = string.Join(" ", new[] { zip }.Where(x => !string.IsNullOrWhiteSpace(x)));

                var combined = string.Join(", ", new[] { firstPart, secondPart }.Where(x => !string.IsNullOrWhiteSpace(x)));
                combined = string.Join(" ", new[] { combined, thirdPart }.Where(x => !string.IsNullOrWhiteSpace(x)));

                if (!string.IsNullOrWhiteSpace(combined))
                    return combined;
            }

            return (appointment.Address ?? string.Empty).Trim();
        }

        private async Task CreateGpsPointFromCheckInAsync(CheckRecord record)
        {
            // Não cria ponto automático se não houver coordenadas válidas.
            if (!IsValidLatLng(record.Latitude, record.Longitude))
                return;

            // Enriquecimento best-effort
            string? companyName = null;
            try
            {
                var company = await _unitOfWork.Companies.GetByIdAsync(record.CompanyId);
                companyName = company?.Name;
            }
            catch { }

            var gpsPoint = new GpsTracking
            {
                ProfessionalId = record.ProfessionalId,
                ProfessionalName = record.ProfessionalName,
                CompanyId = record.CompanyId,
                CompanyName = companyName,
                TeamId = record.TeamId,
                Status = GpsTrackingStatus.Active,
                Source = GpsTrackingSource.CheckIn,
                AppointmentId = record.AppointmentId,
                CustomerId = record.CustomerId,
                CheckRecordId = record.Id,
                Timestamp = record.CheckInTime ?? record.CreatedDate,
                Location = new Location
                {
                    Latitude = (double)record.Latitude!.Value,
                    Longitude = (double)record.Longitude!.Value,
                    Address = record.Address ?? string.Empty
                }
            };

            await _unitOfWork.GpsTrackings.Add(gpsPoint);
            await _unitOfWork.SaveAsync();
        }

        private static bool IsValidLatLng(decimal? lat, decimal? lng)
        {
            if (!lat.HasValue || !lng.HasValue) return false;
            if (lat.Value == 0m && lng.Value == 0m) return false;
            if (lat.Value < -90m || lat.Value > 90m) return false;
            if (lng.Value < -180m || lng.Value > 180m) return false;
            return true;
        }
    }
}
