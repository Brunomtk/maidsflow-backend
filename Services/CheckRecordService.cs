using System;
using System.Threading.Tasks;
using Core.DTO.CheckRecord;
using Core.Enums.CheckRecord;
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
            // CustomerId e AppointmentId são obrigatórios no DTO
            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);
            if (dto.TeamId.HasValue) await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);
            await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId);

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

            // CustomerId e AppointmentId são obrigatórios no DTO
            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);
            if (dto.TeamId.HasValue) await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);
            await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId);

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
    }
}
