using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Infrastructure.Repositories;

namespace Services
{
    public interface IAppointmentCompletionService
    {
        /// <summary>
        /// Records a completion snapshot for an occurrence.
        /// If a snapshot for (AppointmentId, OccurrenceStart) already exists, it returns it without creating a new row.
        /// NOTE: This method does NOT call SaveAsync(); caller controls the transaction.
        /// </summary>
        Task<AppointmentCompletion> RecordCompletionAsync(
            Appointment appointment,
            DateTime occurrenceStart,
            DateTime occurrenceEnd,
            List<int>? professionalIdsOverride = null,
            DateTime? completedAt = null);
    }

    public class AppointmentCompletionService : IAppointmentCompletionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AppointmentCompletionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<AppointmentCompletion> RecordCompletionAsync(
            Appointment appointment,
            DateTime occurrenceStart,
            DateTime occurrenceEnd,
            List<int>? professionalIdsOverride = null,
            DateTime? completedAt = null)
        {
            // Prevent duplicates
            var existing = await _unitOfWork.AppointmentCompletions.GetByAppointmentAndOccurrenceStartAsync(
                appointment.Id, occurrenceStart);

            if (existing != null)
                return existing;

            var professionalIds = (professionalIdsOverride ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (professionalIds.Count == 0 && appointment.ProfessionalIds != null && appointment.ProfessionalIds.Count > 0)
                professionalIds = appointment.ProfessionalIds.Distinct().ToList();

            if (professionalIds.Count == 0 && appointment.TeamId.HasValue)
            {
                var members = await _unitOfWork.Teams.GetMembersByTeamIdAsync(appointment.TeamId.Value);
                professionalIds = members.Select(m => m.ProfessionalId).Distinct().ToList();
            }

            decimal sourceAmount = 0m;
            Customer? customer = null;
            CustomerAddress? address = null;

            if (appointment.CustomerId.HasValue)
                customer = await _unitOfWork.Customers.GetById(appointment.CustomerId.Value);

            if (appointment.CustomerAddressId.HasValue)
                address = await _unitOfWork.CustomerAddresses.GetByIdAsync(appointment.CustomerAddressId.Value);

            if (address == null && appointment.CustomerId.HasValue)
                address = await _unitOfWork.CustomerAddresses.GetPrimaryByCustomerAsync(appointment.CustomerId.Value);

            if (address != null && address.Ticket.HasValue)
                sourceAmount = address.Ticket.Value;
            else if (customer != null)
                sourceAmount = customer.Ticket ?? 0m;

            var completion = new AppointmentCompletion
            {
                CompanyId = appointment.CompanyId,
                AppointmentId = appointment.Id,
                SeriesId = appointment.SeriesId,
                OccurrenceStart = occurrenceStart,
                OccurrenceEnd = occurrenceEnd,
                CompletedAt = completedAt ?? DateTime.UtcNow,
                CustomerIdSnapshot = appointment.CustomerId,
                CustomerAddressIdSnapshot = address?.Id ?? appointment.CustomerAddressId,
                TeamIdSnapshot = appointment.TeamId,
                CategorySnapshot = appointment.Category ?? appointment.Type.ToString(),
                ServiceTypeIdSnapshot = appointment.ServiceTypeId,
                SourceAmountSnapshot = sourceAmount,
                CustomerAddressSnapshot = address != null ? BuildAddressSnapshot(address) : null,
                PaymentMethodSnapshot = address?.PaymentMethod,
                FrequencySnapshot = address?.Frequency,
                ProfessionalIdsSnapshot = professionalIds
            };

            await _unitOfWork.AppointmentCompletions.Add(completion);
            return completion;
        }

        private static string BuildAddressSnapshot(CustomerAddress addr)
        {
            var line1 = addr.AddressLine1?.Trim() ?? string.Empty;
            var line2 = addr.AddressLine2?.Trim();
            var city = addr.City?.Trim();
            var state = addr.State?.Trim();
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(line1)) parts.Add(line1);
            if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2!);
            if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(state))
                parts.Add($"{city}/{state}");
            else
            {
                if (!string.IsNullOrWhiteSpace(city)) parts.Add(city!);
                if (!string.IsNullOrWhiteSpace(state)) parts.Add(state!);
            }
            return string.Join(", ", parts);
        }
    }
}
