using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Issues;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;
using Services.Storage;

namespace Services
{
    public interface IServiceIssueService
    {
        Task<List<ServiceIssue>> GetByCompanyAsync();
        Task<List<ServiceIssue>> GetByAppointmentAsync(int appointmentId);
        Task<ServiceIssue?> GetByIdAsync(int id);
        Task<ServiceIssue> CreateAsync(CreateServiceIssueDTO dto);
        Task<ServiceIssue?> UpdateStatusAsync(int id, UpdateServiceIssueStatusDTO dto);
    }

    public class ServiceIssueService : IServiceIssueService
    {
        private static readonly string[] AllowedTypes =
        {
            "item-damaged",
            "item-lost",
            "access-problem",
            "client-complaint",
            "other"
        };

        private static readonly string[] AllowedStatuses =
        {
            "open",
            "in-review",
            "resolved"
        };

        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IS3StorageService _s3;

        public ServiceIssueService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope, IS3StorageService s3)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
            _s3 = s3;
        }

        public async Task<List<ServiceIssue>> GetByCompanyAsync()
        {
            var companyId = await _scope.GetScopedCompanyIdAsync();
            if (!companyId.HasValue)
                throw new ForbiddenException("A company scope is required.");

            if (_currentUser.IsAdmin || _currentUser.IsCompany)
                return await _uow.ServiceIssues.GetByCompanyAsync(companyId.Value);

            if (_currentUser.IsProfessional)
            {
                var professionalId = await _scope.GetScopedProfessionalIdAsync();
                if (!professionalId.HasValue)
                    throw new ForbiddenException("A professional scope is required.");

                return await _uow.ServiceIssues.GetByProfessionalAsync(companyId.Value, professionalId.Value);
            }

            throw new ForbiddenException("You do not have permission to list issues.");
        }

        public async Task<List<ServiceIssue>> GetByAppointmentAsync(int appointmentId)
        {
            await _scope.EnsureAppointmentAccessAsync(appointmentId);
            return await _uow.ServiceIssues.GetByAppointmentAsync(appointmentId);
        }

        public async Task<ServiceIssue?> GetByIdAsync(int id)
        {
            var issue = await _uow.ServiceIssues.GetByIdAsync(id);
            if (issue == null)
                return null;

            if (_currentUser.IsProfessional)
            {
                await _scope.EnsureAppointmentAccessAsync(issue.AppointmentId);
                return issue;
            }

            await _scope.EnsureCompanyAccessAsync(issue.CompanyId);
            return issue;
        }

        public async Task<ServiceIssue> CreateAsync(CreateServiceIssueDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany && !_currentUser.IsProfessional)
                throw new ForbiddenException("You do not have permission to create issues.");

            await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId);

            var appointment = await _uow.Appointments.GetById(dto.AppointmentId);
            if (appointment == null)
                throw new BadRequestException("Appointment not found.");

            var normalizedType = NormalizeType(dto.Type);
            var issue = new ServiceIssue
            {
                CompanyId = appointment.CompanyId,
                AppointmentId = appointment.Id,
                CustomerId = appointment.CustomerId,
                CustomerAddressId = appointment.CustomerAddressId,
                ProfessionalId = await ResolveReportedProfessionalIdAsync(),
                ReportedByUserId = _currentUser.UserId,
                Type = normalizedType,
                Status = "open",
                Summary = (dto.Summary ?? string.Empty).Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                EstimatedAmount = dto.EstimatedAmount,
                PhotoUrls = NormalizePhotoValues(dto.PhotoKeys ?? dto.PhotoUrls)
            };

            await _uow.ServiceIssues.Add(issue);
            await _uow.SaveAsync();
            return issue;
        }

        public async Task<ServiceIssue?> UpdateStatusAsync(int id, UpdateServiceIssueStatusDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("You do not have permission to update issue status.");

            var issue = await _uow.ServiceIssues.GetByIdAsync(id);
            if (issue == null)
                return null;

            await _scope.EnsureCompanyAccessAsync(issue.CompanyId);

            issue.Status = NormalizeStatus(dto.Status);
            issue.InternalNotes = string.IsNullOrWhiteSpace(dto.InternalNotes) ? issue.InternalNotes : dto.InternalNotes.Trim();
            issue.ApprovedAmount = dto.ApprovedAmount ?? issue.ApprovedAmount;
            issue.ReviewedByUserId = _currentUser.UserId > 0 ? _currentUser.UserId : issue.ReviewedByUserId;
            issue.UpdatedDate = DateTime.UtcNow;

            if (issue.Status == "resolved")
                issue.ResolvedAtUtc = DateTime.UtcNow;

            _uow.ServiceIssues.Update(issue);
            await _uow.SaveAsync();
            return issue;
        }

        private string NormalizeType(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedTypes.Contains(normalized))
                throw new BadRequestException("Invalid issue type.");

            return normalized;
        }

        private string NormalizeStatus(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedStatuses.Contains(normalized))
                throw new BadRequestException("Invalid issue status.");

            return normalized;
        }

        private List<string> NormalizePhotoValues(List<string>? values)
        {
            if (values == null || values.Count == 0) return new List<string>();

            return values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Select(x => _s3.TryGetKeyFromStoredValue(x, out var key) ? key : x)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<int?> ResolveReportedProfessionalIdAsync()
        {
            if (_currentUser.IsProfessional)
                return await _scope.GetScopedProfessionalIdAsync();

            return null;
        }
    }
}
