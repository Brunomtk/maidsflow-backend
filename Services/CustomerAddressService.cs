using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Exceptions;
using Core.DTO.Customer;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services
{
    public interface ICustomerAddressService
    {
        Task<List<CustomerAddress>> GetByCustomerAsync(int customerId);
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

        public CustomerAddressService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<CustomerAddress>> GetByCustomerAsync(int customerId)
        {
            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return new List<CustomerAddress>();

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            return await _uow.CustomerAddresses.GetByCustomerAsync(customerId);
        }

        public async Task<CustomerAddress?> CreateAsync(int customerId, CreateCustomerAddressDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para criar endereços.");

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
                Observations = string.IsNullOrWhiteSpace(dto.Observations) ? null : dto.Observations.Trim(),
                Ticket = dto.Ticket,
                Frequency = string.IsNullOrWhiteSpace(dto.Frequency) ? null : dto.Frequency.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? null : dto.PaymentMethod.Trim(),
                IsPrimary = dto.IsPrimary
            };

            if (addr.IsPrimary)
                await ClearPrimaryAsync(customerId);

            await _uow.CustomerAddresses.Add(addr);
            var saved = await _uow.SaveAsync();
            if (saved <= 0) return null;

            if (addr.IsPrimary)
                await SyncCustomerLegacyFromPrimaryAsync(customerId, addr);

            return addr;
        }

        public async Task<CustomerAddress?> UpdateAsync(int customerId, int addressId, UpdateCustomerAddressDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para editar endereços.");

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
            if (dto.Observations != null) addr.Observations = string.IsNullOrWhiteSpace(dto.Observations) ? null : dto.Observations.Trim();
            if (dto.Ticket.HasValue) addr.Ticket = dto.Ticket;
            if (dto.Frequency != null) addr.Frequency = string.IsNullOrWhiteSpace(dto.Frequency) ? null : dto.Frequency.Trim();
            if (dto.PaymentMethod != null) addr.PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? null : dto.PaymentMethod.Trim();
            addr.UpdatedDate = DateTime.UtcNow;

            _uow.CustomerAddresses.Update(addr);
            var saved = await _uow.SaveAsync();
            if (saved <= 0) return null;

            if (addr.IsPrimary)
                await SyncCustomerLegacyFromPrimaryAsync(customerId, addr);

            return addr;
        }

        public async Task<bool> DeleteAsync(int customerId, int addressId)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para remover endereços.");

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
                throw new ForbiddenException("Você não tem permissão para definir endereço principal.");

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

        private async Task SyncCustomerLegacyFromPrimaryAsync(int customerId, CustomerAddress addr)
        {
            var customer = await _uow.Customers.GetByIdAsync(customerId);
            if (customer == null) return;

            customer.Address = addr.AddressLine1;
            customer.City = addr.City;
            customer.State = addr.State;
            customer.ZipCode = addr.ZipCode;
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
