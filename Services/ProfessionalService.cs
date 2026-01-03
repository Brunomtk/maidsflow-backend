using Core.DTO.Professional;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.ServiceExtension;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public class ProfessionalService : IProfessionalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ProfessionalService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<Professional>> GetAllProfessionals()
        {
            // Admin: all
            if (_currentUser.IsAdmin)
                return (await _unitOfWork.Professionals.GetAll()).ToList();

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            // Company: all in company
            if (_currentUser.IsCompany)
            {
                var all = (await _unitOfWork.Professionals.GetAll()).ToList();
                return all.Where(p => p.CompanyId == scopedCompanyId.Value).ToList();
            }

            // Professional: only self
            if (_currentUser.IsProfessional)
            {
                var profId = await _scope.GetScopedProfessionalIdAsync();
                if (!profId.HasValue) return new List<Professional>();
                var prof = await _unitOfWork.Professionals.GetById(profId.Value);
                return prof != null ? new List<Professional> { prof } : new List<Professional>();
            }

            throw new ForbiddenException();
        }

        public async Task<Professional?> GetProfessionalById(int id)
        {
            // Enforce professional/company scoping
            await _scope.EnsureProfessionalAccessAsync(id);
            return await _unitOfWork.Professionals.GetById(id);
        }

        public async Task<Professional> CreateProfessional(CreateProfessionalRequest request)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para criar profissionais.");

            // company scope
            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!_currentUser.IsAdmin)
            {
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                request.CompanyId = scopedCompanyId.Value;
            }

            // validate team belongs to company when provided (company scope)
            if (request.TeamId.HasValue)
            {
                await _scope.EnsureTeamInCompanyAsync(request.TeamId.Value);
            }

            var professional = new Professional
            {
                Name = request.Name,
                Cpf = request.Cpf,
                Email = request.Email,
                Phone = request.Phone,
                Status = StatusEnum.Active,
                TeamId = request.TeamId,
                CompanyId = request.CompanyId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Professionals.Add(professional);
            _unitOfWork.Save();

            return professional;
        }

        public async Task<Professional?> UpdateProfessional(int id, UpdateProfessionalRequest request)
        {
            // Admin: any
            if (_currentUser.IsAdmin)
            {
                var professional = await _unitOfWork.Professionals.GetById(id);
                if (professional == null) return null;

                professional.Name = request.Name ?? professional.Name;
                professional.Cpf = request.Cpf ?? professional.Cpf;
                professional.Email = request.Email ?? professional.Email;
                professional.Phone = request.Phone ?? professional.Phone;
                professional.TeamId = request.TeamId ?? professional.TeamId;
                professional.Status = request.Status ?? professional.Status;
                professional.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Professionals.Update(professional);
                _unitOfWork.Save();
                return professional;
            }

            // Company: can update any professional in its company
            if (_currentUser.IsCompany)
            {
                await _scope.EnsureProfessionalAccessAsync(id);

                var professional = await _unitOfWork.Professionals.GetById(id);
                if (professional == null) return null;

                // validate team belongs to company when changed
                if (request.TeamId.HasValue)
                    await _scope.EnsureTeamInCompanyAsync(request.TeamId.Value);

                professional.Name = request.Name ?? professional.Name;
                professional.Cpf = request.Cpf ?? professional.Cpf;
                professional.Email = request.Email ?? professional.Email;
                professional.Phone = request.Phone ?? professional.Phone;
                professional.TeamId = request.TeamId ?? professional.TeamId;
                professional.Status = request.Status ?? professional.Status;
                professional.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Professionals.Update(professional);
                _unitOfWork.Save();
                return professional;
            }

            // Professional: only self (and very limited changes)
            if (_currentUser.IsProfessional)
            {
                var scopedProfId = await _scope.GetScopedProfessionalIdAsync();
                if (!scopedProfId.HasValue || scopedProfId.Value != id)
                    throw new ForbiddenException("Você não tem permissão para atualizar este profissional.");

                var professional = await _unitOfWork.Professionals.GetById(id);
                if (professional == null) return null;

                // allow updating basic profile fields only
                professional.Name = request.Name ?? professional.Name;
                professional.Email = request.Email ?? professional.Email;
                professional.Phone = request.Phone ?? professional.Phone;
                professional.UpdatedDate = DateTime.UtcNow;

                _unitOfWork.Professionals.Update(professional);
                _unitOfWork.Save();
                return professional;
            }

            throw new ForbiddenException();
        }

        public async Task<bool> DeleteProfessional(int id)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para remover profissionais.");

            await _scope.EnsureProfessionalAccessAsync(id);

            var professional = await _unitOfWork.Professionals.GetById(id);
            if (professional == null) return false;

            _unitOfWork.Professionals.Delete(professional);
            _unitOfWork.Save();

            return true;
        }

        public async Task<PagedResult<Professional>> GetPagedProfessionals(ProfessionalFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                filters.CompanyId = scopedCompanyId.Value;

                // Professional role only sees itself
                if (_currentUser.IsProfessional)
                {
                    var profId = await _scope.GetScopedProfessionalIdAsync();
                    if (!profId.HasValue)
                    {
                        return new PagedResult<Professional>
                        {
                            CurrentPage = filters.Page,
                            PageSize = filters.PageSize,
                            PageCount = 0,
                            TotalItems = 0,
                            Results = new List<Professional>()
                        };
                    }

                    var prof = await _unitOfWork.Professionals.GetById(profId.Value);
                    if (prof == null)
                    {
                        return new PagedResult<Professional>
                        {
                            CurrentPage = filters.Page,
                            PageSize = filters.PageSize,
                            PageCount = 0,
                            TotalItems = 0,
                            Results = new List<Professional>()
                        };
                    }

                    // Ensure professional is still inside scoped company
                    await _scope.EnsureCompanyAccessAsync(prof.CompanyId);

                    return new PagedResult<Professional>
                    {
                        CurrentPage = 1,
                        PageSize = filters.PageSize,
                        PageCount = 1,
                        TotalItems = 1,
                        Results = new List<Professional> { prof }
                    };
                }
            }

            return await _unitOfWork.Professionals.GetPagedProfessionalsAsync(filters);
        }
    }

    public interface IProfessionalService
    {
        Task<List<Professional>> GetAllProfessionals();
        Task<Professional?> GetProfessionalById(int id);
        Task<Professional> CreateProfessional(CreateProfessionalRequest request);
        Task<Professional?> UpdateProfessional(int id, UpdateProfessionalRequest request);
        Task<bool> DeleteProfessional(int id);
        Task<PagedResult<Professional>> GetPagedProfessionals(ProfessionalFiltersDTO filters);
    }
}
