using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.CheckRecord;
using Core.Enums.CheckRecord;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services;

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

        public CheckRecordService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CheckRecord?> GetByIdAsync(int id)
        {
            return await _unitOfWork.CheckRecords.GetByIdAsync(id);
        }

        public async Task<PagedResult<CheckRecord>> GetPagedAsync(CheckRecordFiltersDTO filters)
        {
            return await _unitOfWork.CheckRecords.GetPagedAsync(filters);
        }

        public async Task<CheckRecord> CreateAsync(CreateCheckRecordDTO dto)
        {
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
            var record = await _unitOfWork.CheckRecords.GetByIdAsync(id);
            if (record == null) return null;

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
            var record = await _unitOfWork.CheckRecords.GetByIdAsync(id);
            if (record == null) return false;

            _unitOfWork.CheckRecords.Delete(record);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<CheckRecord> PerformCheckInAsync(CreateCheckRecordDTO dto)
        {
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

            record.CheckOutTime = DateTime.UtcNow;
            record.Status = CheckRecordStatus.CheckedOut;
            record.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.CheckRecords.Update(record);
            await _unitOfWork.SaveAsync();

            return record;
        }
    }
}
