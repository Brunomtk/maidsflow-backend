using Core.DTO.Customer;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public interface ICustomerService
    {
        Task<Customer?> GetByIdAsync(int id);
        Task<PagedResult<Customer>> GetPagedAsync(CustomerFiltersDTO filters);
        Task<Customer?> CreateAsync(Customer customer);
        Task<bool> UpdateAsync(Customer customer);
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Importação em lote (bulk) de clientes.
        /// Permite DryRun (apenas validar), e retorna erros por linha.
        /// </summary>
        Task<BulkCreateCustomersResponse> BulkCreateAsync(BulkCreateCustomersRequest request);
    }

    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public CustomerService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<Customer?> GetByIdAsync(int id)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customer == null) return null;

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            return customer;
        }

        public async Task<PagedResult<Customer>> GetPagedAsync(CustomerFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                filters.CompanyId = scopedCompanyId.Value;
            }

            return await _unitOfWork.Customers.GetPagedCustomersAsync(filters);
        }

        public async Task<Customer?> CreateAsync(Customer customer)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para criar clientes.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                customer.CompanyId = scopedCompanyId.Value;
            }

            await _unitOfWork.Customers.Add(customer);
            var result = await _unitOfWork.SaveAsync();
            return result > 0 ? customer : null;
        }

        public async Task<bool> UpdateAsync(Customer customer)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para editar clientes.");

            var existing = await _unitOfWork.Customers.GetByIdAsync(customer.Id);
            if (existing == null) return false;

            await _scope.EnsureCompanyAccessAsync(existing.CompanyId);

            // company users cannot move customers between companies
            if (!_currentUser.IsAdmin)
                customer.CompanyId = existing.CompanyId;

            _unitOfWork.Customers.Update(customer);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para remover clientes.");

            var existing = await _unitOfWork.Customers.GetByIdAsync(id);
            if (existing == null) return false;

            await _scope.EnsureCompanyAccessAsync(existing.CompanyId);

            _unitOfWork.Customers.Delete(existing);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<BulkCreateCustomersResponse> BulkCreateAsync(BulkCreateCustomersRequest request)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para importar clientes.");

            request ??= new BulkCreateCustomersRequest();

            var rows = request.Rows ?? new List<BulkCreateCustomerRowDTO>();

            var response = new BulkCreateCustomersResponse
            {
                TotalRows = rows.Count
            };

            if (rows.Count == 0)
                return response;

            int companyId;

            if (_currentUser.IsAdmin)
            {
                if (!request.CompanyId.HasValue || request.CompanyId.Value <= 0)
                    throw new BadRequestException("Para Admin, 'companyId' é obrigatório na importação em lote.");

                companyId = request.CompanyId.Value;
            }
            else
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                companyId = scopedCompanyId.Value;
            }

            static string? DigitsOnly(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var chars = s.Where(char.IsDigit).ToArray();
                var d = new string(chars);
                return string.IsNullOrWhiteSpace(d) ? null : d;
            }

            var toCreate = new List<Customer>();

            for (var i = 0; i < rows.Count; i++)
            {
                var rowNumber = i + 1; // 1-based (linha na planilha após cabeçalho)
                var row = rows[i] ?? new BulkCreateCustomerRowDTO();

                var name = (row.Name ?? string.Empty).Trim();
                var address = (row.Address ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    response.Errors.Add(new BulkCreateCustomerErrorDTO { RowNumber = rowNumber, Field = "name", Message = "Nome é obrigatório." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    response.Errors.Add(new BulkCreateCustomerErrorDTO { RowNumber = rowNumber, Field = "address", Message = "Endereço é obrigatório." });
                    continue;
                }

                var ssnDigits = DigitsOnly(row.Ssn);
                if (ssnDigits != null && ssnDigits.Length > 11)
                {
                    response.Errors.Add(new BulkCreateCustomerErrorDTO { RowNumber = rowNumber, Field = "ssn", Message = "SSN inválido (máximo 11 caracteres)." });
                    continue;
                }

                var state = (row.State ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(state) && state.Length != 2)
                {
                    response.Errors.Add(new BulkCreateCustomerErrorDTO { RowNumber = rowNumber, Field = "state", Message = "State deve ter 2 letras (ex.: CA, NY)." });
                    continue;
                }

                var customer = new Customer
                {
                    Name = name,
                    Ssn = ssnDigits,
                    Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim(),
                    Phone = row.Phone?.Trim() ?? string.Empty,
                    Address = address,
                    ZipCode = string.IsNullOrWhiteSpace(row.ZipCode) ? null : row.ZipCode.Trim(),
                    City = row.City?.Trim() ?? string.Empty,
                    State = state,
                    Observations = string.IsNullOrWhiteSpace(row.Observations) ? null : row.Observations.Trim(),
                    Ticket = row.Ticket,
                    Frequency = string.IsNullOrWhiteSpace(row.Frequency) ? null : row.Frequency.Trim(),
                    PaymentMethod = string.IsNullOrWhiteSpace(row.PaymentMethod) ? null : row.PaymentMethod.Trim(),
                    ReceiveSms = row.ReceiveSms ?? true,
                    ReceiveEmail = row.ReceiveEmail ?? true,
                    CompanyId = companyId
                };

                toCreate.Add(customer);
            }

            response.ErrorCount = response.Errors.Count;

            if (request.DryRun)
            {
                response.CreatedCount = 0;
                return response;
            }

            if (toCreate.Count == 0)
            {
                response.CreatedCount = 0;
                return response;
            }

            await _unitOfWork.Customers.AddRangeAsync(toCreate);
            var saved = await _unitOfWork.SaveAsync();

            // SaveAsync retorna quantidade de entidades alteradas; não é exatamente igual ao count.
            // O que importa pra UX é o total de linhas válidas processadas.
            _ = saved;
            response.CreatedCount = toCreate.Count;
            return response;
        }
    }
}
