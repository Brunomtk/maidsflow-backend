using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Exceptions;
using Core.DTO.Customer;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;
using Services.Storage;

namespace Services
{
    public interface ICustomerAddressService
    {
        Task<List<CustomerAddress>> GetByCustomerAsync(int customerId);
        Task<CustomerAddress?> GetByIdForCustomerAsync(int customerId, int addressId);
        Task<CustomerAddress?> CreateAsync(int customerId, CreateCustomerAddressDTO dto);
        Task<CustomerAddress?> UpdateAsync(int customerId, int addressId, UpdateCustomerAddressDTO dto);
        Task<bool> DeleteAsync(int customerId, int addressId);
        Task<bool> SetPrimaryAsync(int customerId, int addressId);
    }

    public class CustomerAddressService : ICustomerAddressService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IS3StorageService _s3;

        public CustomerAddressService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope, IS3StorageService s3)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
            _s3 = s3;
        }

        public async Task<CustomerAddress?> GetByIdForCustomerAsync(int customerId, int addressId)
        {
            if (_currentUser.IsPropertyManager)
                await _scope.EnsureCustomerInCompanyAsync(customerId);

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return null;

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);

            var address = await _uow.CustomerAddresses.GetByIdAsync(addressId);
            if (address == null || address.CustomerId != customerId) return null;

            return address;
        }

        public async Task<List<CustomerAddress>> GetByCustomerAsync(int customerId)
        {
            // Property Manager can only read addresses from its scoped customer
            if (_currentUser.IsPropertyManager)
                await _scope.EnsureCustomerInCompanyAsync(customerId);

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return new List<CustomerAddress>();

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            return await _uow.CustomerAddresses.GetByCustomerAsync(customerId);
        }

        public async Task<CustomerAddress?> CreateAsync(int customerId, CreateCustomerAddressDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("You do not have permission to create addresses.");

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return null;

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);

            var addr = new CustomerAddress
            {
                CustomerId = customerId,
                Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim(),
                AddressLine1 = (dto.AddressLine1 ?? string.Empty).Trim(),
                AddressLine2 = string.IsNullOrWhiteSpace(dto.AddressLine2) ? null : dto.AddressLine2.Trim(),
                City = (dto.City ?? string.Empty).Trim(),
                State = (dto.State ?? string.Empty).Trim(),
                ZipCode = string.IsNullOrWhiteSpace(dto.ZipCode) ? null : dto.ZipCode.Trim(),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                Phone2 = string.IsNullOrWhiteSpace(dto.Phone2) ? null : dto.Phone2.Trim(),
                Observations = string.IsNullOrWhiteSpace(dto.Observations) ? null : dto.Observations.Trim(),
                Ticket = dto.Ticket,
                Frequency = string.IsNullOrWhiteSpace(dto.Frequency) ? null : dto.Frequency.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? null : dto.PaymentMethod.Trim(),
                IsPrimary = dto.IsPrimary
            };

            ApplyHouseNotes(addr, dto.HouseAccessNotes, dto.HouseGateCode, dto.HouseHasPets, dto.HousePetNotes, dto.HouseRestrictionsNotes, dto.HousePriorityNotes, dto.HousePhotoUrls);

            if (addr.IsPrimary)
                await ClearPrimaryAsync(customerId);

            await _uow.CustomerAddresses.Add(addr);
            var saved = await _uow.SaveAsync();
            if (saved <= 0) return null;

            if (addr.IsPrimary)
                await SyncCustomerLegacyFromPrimaryAsync(customerId, addr);

            await SyncRelatedHouseNotesSnapshotsAsync(addr);

            return addr;
        }

        public async Task<CustomerAddress?> UpdateAsync(int customerId, int addressId, UpdateCustomerAddressDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("You do not have permission to update addresses.");

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return null;
            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);

            var addr = await _uow.CustomerAddresses.GetByIdAsync(addressId);
            if (addr == null || addr.CustomerId != customerId) return null;

            if (dto.Label != null) addr.Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim();
            if (dto.AddressLine1 != null) addr.AddressLine1 = (dto.AddressLine1 ?? string.Empty).Trim();
            if (dto.AddressLine2 != null) addr.AddressLine2 = string.IsNullOrWhiteSpace(dto.AddressLine2) ? null : dto.AddressLine2.Trim();
            if (dto.City != null) addr.City = (dto.City ?? string.Empty).Trim();
            if (dto.State != null) addr.State = (dto.State ?? string.Empty).Trim();
            if (dto.ZipCode != null) addr.ZipCode = string.IsNullOrWhiteSpace(dto.ZipCode) ? null : dto.ZipCode.Trim();
            if (dto.Phone != null) addr.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            if (dto.Phone2 != null) addr.Phone2 = string.IsNullOrWhiteSpace(dto.Phone2) ? null : dto.Phone2.Trim();
            if (dto.Observations != null) addr.Observations = string.IsNullOrWhiteSpace(dto.Observations) ? null : dto.Observations.Trim();
            if (dto.Ticket.HasValue) addr.Ticket = dto.Ticket;
            if (dto.Frequency != null) addr.Frequency = string.IsNullOrWhiteSpace(dto.Frequency) ? null : dto.Frequency.Trim();
            if (dto.PaymentMethod != null) addr.PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? null : dto.PaymentMethod.Trim();
            if (dto.HouseAccessNotes != null) addr.HouseAccessNotes = Clean(dto.HouseAccessNotes, 600);
            if (dto.HouseGateCode != null) addr.HouseGateCode = Clean(dto.HouseGateCode, 120);
            if (dto.HouseHasPets.HasValue) addr.HouseHasPets = dto.HouseHasPets;
            if (dto.HousePetNotes != null) addr.HousePetNotes = Clean(dto.HousePetNotes, 600);
            if (dto.HouseRestrictionsNotes != null) addr.HouseRestrictionsNotes = Clean(dto.HouseRestrictionsNotes, 800);
            if (dto.HousePriorityNotes != null) addr.HousePriorityNotes = Clean(dto.HousePriorityNotes, 800);
            if (dto.HousePhotoUrls != null) addr.HousePhotoUrls = NormalizePhotoValues(dto.HousePhotoUrls);
            addr.UpdatedDate = DateTime.UtcNow;

            _uow.CustomerAddresses.Update(addr);
            var saved = await _uow.SaveAsync();
            if (saved <= 0) return null;

            if (addr.IsPrimary)
                await SyncCustomerLegacyFromPrimaryAsync(customerId, addr);

            await SyncRelatedHouseNotesSnapshotsAsync(addr);

            return addr;
        }

        public async Task<bool> DeleteAsync(int customerId, int addressId)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("You do not have permission to delete addresses.");

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return false;
            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);

            var addr = await _uow.CustomerAddresses.GetByIdAsync(addressId);
            if (addr == null || addr.CustomerId != customerId) return false;

            var wasPrimary = addr.IsPrimary;
            _uow.CustomerAddresses.Delete(addr);
            var saved = await _uow.SaveAsync();
            if (saved <= 0) return false;

            if (wasPrimary)
            {
                var remaining = await _uow.CustomerAddresses.GetByCustomerAsync(customerId);
                var next = remaining.OrderByDescending(a => a.CreatedDate).FirstOrDefault();
                if (next != null)
                {
                    await SetPrimaryAsync(customerId, next.Id);
                }
            }

            return true;
        }

        public async Task<bool> SetPrimaryAsync(int customerId, int addressId)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("You do not have permission to set the primary address.");

            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return false;
            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);

            var addr = await _uow.CustomerAddresses.GetByIdAsync(addressId);
            if (addr == null || addr.CustomerId != customerId) return false;

            await ClearPrimaryAsync(customerId);

            addr.IsPrimary = true;
            addr.UpdatedDate = DateTime.UtcNow;
            _uow.CustomerAddresses.Update(addr);

            var saved = await _uow.SaveAsync();
            if (saved <= 0) return false;

            await SyncCustomerLegacyFromPrimaryAsync(customerId, addr);
            return true;
        }

        private async Task ClearPrimaryAsync(int customerId)
        {
            var addresses = await _uow.CustomerAddresses.GetByCustomerAsync(customerId);
            var primaries = addresses.Where(a => a.IsPrimary).ToList();
            if (primaries.Count == 0) return;

            foreach (var p in primaries)
            {
                p.IsPrimary = false;
                p.UpdatedDate = DateTime.UtcNow;
                _uow.CustomerAddresses.Update(p);
            }

            await _uow.SaveAsync();
        }

        private void ApplyHouseNotes(CustomerAddress address, string? accessNotes, string? gateCode, bool? hasPets, string? petNotes, string? restrictionsNotes, string? priorityNotes, List<string>? photoUrls)
        {
            address.HouseAccessNotes = Clean(accessNotes, 600);
            address.HouseGateCode = Clean(gateCode, 120);
            address.HouseHasPets = hasPets;
            address.HousePetNotes = Clean(petNotes, 600);
            address.HouseRestrictionsNotes = Clean(restrictionsNotes, 800);
            address.HousePriorityNotes = Clean(priorityNotes, 800);
            address.HousePhotoUrls = NormalizePhotoValues(photoUrls);
        }


        private List<string> NormalizePhotoValues(List<string>? photoUrls)
        {
            if (photoUrls == null || photoUrls.Count == 0)
                return new List<string>();

            var normalized = new List<string>();
            foreach (var item in photoUrls)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                var trimmed = item.Trim();
                if (_s3.TryGetKeyFromStoredValue(trimmed, out var key) && !string.IsNullOrWhiteSpace(key))
                    normalized.Add(key);
                else
                    normalized.Add(trimmed);
            }

            return normalized
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? Clean(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength);
        }

        private async Task SyncRelatedHouseNotesSnapshotsAsync(CustomerAddress addr)
        {
            try
            {
                var appointments = (await _uow.Appointments.GetAppointmentsByCustomerAsync(addr.CustomerId))
                    .Where(x => x.CustomerAddressId == addr.Id)
                    .ToList();

                if (appointments.Count == 0)
                    return;

                var snapshot = BuildHouseNotesSnapshotJson(addr);
                var changed = false;

                foreach (var appointment in appointments)
                {
                    if (appointment.HouseNotesSnapshotJson == snapshot)
                        continue;

                    appointment.HouseNotesSnapshotJson = snapshot;
                    appointment.UpdatedDate = DateTime.UtcNow;
                    _uow.Appointments.Update(appointment);
                    changed = true;
                }

                if (changed)
                    await _uow.SaveAsync();
            }
            catch
            {
                // Best-effort sync only. Address save should not fail because of snapshot propagation.
            }
        }

        private static string? BuildHouseNotesSnapshotJson(CustomerAddress address)
        {
            var hasAny =
                !string.IsNullOrWhiteSpace(address.HouseAccessNotes) ||
                !string.IsNullOrWhiteSpace(address.HouseGateCode) ||
                address.HouseHasPets.HasValue ||
                !string.IsNullOrWhiteSpace(address.HousePetNotes) ||
                !string.IsNullOrWhiteSpace(address.HouseRestrictionsNotes) ||
                !string.IsNullOrWhiteSpace(address.HousePriorityNotes) ||
                (address.HousePhotoUrls?.Count ?? 0) > 0;

            if (!hasAny)
                return null;

            var snapshot = new HouseNotesSnapshotDTO
            {
                CustomerAddressId = address.Id,
                AccessNotes = address.HouseAccessNotes,
                GateCode = address.HouseGateCode,
                HasPets = address.HouseHasPets,
                PetNotes = address.HousePetNotes,
                RestrictionsNotes = address.HouseRestrictionsNotes,
                PriorityNotes = address.HousePriorityNotes,
                PhotoUrls = address.HousePhotoUrls
            };

            return System.Text.Json.JsonSerializer.Serialize(snapshot);
        }

        private async Task SyncCustomerLegacyFromPrimaryAsync(int customerId, CustomerAddress addr)
        {
            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return;

            customer.Address = addr.AddressLine1;
            customer.City = addr.City;
            customer.State = addr.State;
            customer.ZipCode = addr.ZipCode;
            if (!string.IsNullOrWhiteSpace(addr.Phone)) customer.Phone = addr.Phone.Trim();
            customer.Phone2 = string.IsNullOrWhiteSpace(addr.Phone2) ? customer.Phone2 : addr.Phone2.Trim();
            customer.Observations = addr.Observations;
            customer.Ticket = addr.Ticket;
            customer.Frequency = addr.Frequency;
            customer.PaymentMethod = addr.PaymentMethod;
            customer.UpdatedDate = DateTime.UtcNow;

            _uow.Customers.Update(customer);
            await _uow.SaveAsync();
        }
    }
}
