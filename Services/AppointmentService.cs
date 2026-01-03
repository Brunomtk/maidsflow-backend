using Core.DTO.Appointment;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Services.Security;
using Core.Exceptions;

namespace Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public AppointmentService(Infrastructure.Repositories.IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<Appointment>> GetPagedAppointments(AppointmentFiltersDTO filters)
{
    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) filters.CompanyId = companyId.Value;

        if (_currentUser.IsProfessional)
        {
            var professionalId = await _scope.GetScopedProfessionalIdAsync();
            if (professionalId.HasValue) filters.ProfessionalId = professionalId.Value;
        }
    }

    return await _unitOfWork.Appointments.GetPagedAppointmentsAsync(filters);
}


        public async Task<List<Appointment>> GetByCompany(int companyId)
{
    if (!_currentUser.IsAdmin)
    {
        await _scope.EnsureCompanyAccessAsync(companyId);
        if (_currentUser.IsProfessional)
        {
            // Para professional, não expõe lista completa da company.
            var professionalId = await _scope.GetScopedProfessionalIdAsync();
            if (!professionalId.HasValue) throw new ForbiddenException("Escopo de profissional inválido.");
            return await _unitOfWork.Appointments.GetAppointmentsByProfessionalAsync(professionalId.Value);
        }
    }

    return await _unitOfWork.Appointments.GetAppointmentsByCompanyAsync(companyId);
}


        public async Task<List<Appointment>> GetByTeam(int teamId)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não pode listar agendamentos por time.");

    if (!_currentUser.IsAdmin)
    {
        // ITeamRepository expõe GetById (via IGenericRepository). Aqui usamos ele para evitar
        // inconsistência de nomenclatura (GetByIdAsync não existe em ITeamRepository).
        var team = await _unitOfWork.Teams.GetById(teamId);
        if (team == null) return new List<Appointment>();
        await _scope.EnsureCompanyAccessAsync(team.CompanyId);
    }

    return await _unitOfWork.Appointments.GetAppointmentsByTeamAsync(teamId);
}


        public async Task<List<Appointment>> GetByProfessional(int professionalId)
{
    await _scope.EnsureProfessionalAccessAsync(professionalId);
    return await _unitOfWork.Appointments.GetAppointmentsByProfessionalAsync(professionalId);
}


        public async Task<List<Appointment>> GetByCustomer(int customerId)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não pode listar agendamentos por cliente.");

    if (!_currentUser.IsAdmin)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
        if (customer == null) return new List<Appointment>();
        await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
    }

    return await _unitOfWork.Appointments.GetAppointmentsByCustomerAsync(customerId);
}


        public async Task<List<Appointment>> GetByDateRange(DateTime start, DateTime end, int? companyId = null)
{
    if (_currentUser.IsProfessional)
    {
        var professionalId = await _scope.GetScopedProfessionalIdAsync();
        if (!professionalId.HasValue) throw new ForbiddenException("Escopo de profissional inválido.");
        var list = await _unitOfWork.Appointments.GetAppointmentsByProfessionalAsync(professionalId.Value);
        return list.Where(a => a.Start >= start && a.End <= end).ToList();
    }

    if (!_currentUser.IsAdmin)
    {
        var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
        companyId = scopedCompanyId;
    }

    return await _unitOfWork.Appointments.GetAppointmentsByDateRangeAsync(start, end, companyId);
}


        public async Task<Appointment?> GetById(int id)
{
    var appointment = await _unitOfWork.Appointments.GetById(id);
    if (appointment == null) return null;

    if (!_currentUser.IsAdmin)
    {
        await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

        if (_currentUser.IsProfessional)
        {
            var professionalId = await _scope.GetScopedProfessionalIdAsync();
            if (!professionalId.HasValue || !appointment.ProfessionalIds.Contains(professionalId.Value))
                throw new ForbiddenException("Você não tem permissão para acessar este agendamento.");
        }
    }

    return appointment;
}


        public async Task<bool> Create(CreateAppointmentDTO dto)
{
    // Professional não cria agendamento (regra simples e segura, sem mudar front)
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para criar agendamentos.");

    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) dto.CompanyId = companyId.Value;
    }

            // Nesta versão, NÃO fazemos conversão de fuso horário.
            //
            // Recorrência com 1 INSERT:
            // - se IsRecurring=true, criamos um SeriesId e persistimos apenas o registro âncora
            // - as ocorrências são expandidas na leitura (endpoint /api/AppointmentsRecurrence/calendar)

            if (dto.IsRecurring && string.IsNullOrWhiteSpace(dto.RecurrenceRule))
                return false;

// Nesta versão, NÃO fazemos conversão de fuso horário.
            // O horário enviado no DTO (start/end) é salvo exatamente como veio,
            // para que o banco sempre reflita o horário local informado pelo usuário.

            var appointment = new Appointment
            {
                Title = dto.Title,
                Address = dto.Address,
                Start = dto.Start,
                End = dto.End,
                Notes = dto.Notes,
                Status = dto.Status ?? AppointmentStatus.Scheduled,
                Type = dto.Type ?? AppointmentType.Regular,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                TeamId = dto.TeamId,
                TimeZoneId = dto.TimeZoneId,
                IsRecurring = dto.IsRecurring,
                RecurrenceRule = dto.RecurrenceRule,
                SeriesId = dto.IsRecurring ? Guid.NewGuid() : null,
                RecurrenceEnd = dto.RecurrenceEnd,
                OccurrenceCount = dto.OccurrenceCount
            };

            if (dto.ProfessionalIds != null)
            {
                appointment.ProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
            }

            await _unitOfWork.Appointments.Add(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }

        public async Task<bool> Update(int id, UpdateAppointmentDTO dto)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para editar agendamentos.");


            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

            // Atualiza campos básicos
            appointment.Title = dto.Title ?? appointment.Title;
            appointment.Address = dto.Address ?? appointment.Address;

            // Aqui também NÃO fazemos conversão de fuso horário.
            // Se vier start/end no DTO, salvamos exatamente o que veio.
            if (dto.Start.HasValue)
            {
                appointment.Start = dto.Start.Value;
            }

            if (dto.End.HasValue)
            {
                appointment.End = dto.End.Value;
            }

            appointment.Notes = dto.Notes ?? appointment.Notes;
            appointment.Status = dto.Status ?? appointment.Status;
            appointment.Type = dto.Type ?? appointment.Type;
            _ = dto.CompanyId; // CompanyId não é alterável aqui
            // mantemos CompanyId existente
            appointment.CompanyId = appointment.CompanyId;
            appointment.CustomerId = dto.CustomerId ?? appointment.CustomerId;
            appointment.TeamId = dto.TeamId ?? appointment.TeamId;

            if (dto.ProfessionalIds != null)
            {
                appointment.ProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
            }

            // Atualiza campos de recorrência/timezone se vierem
            if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
                appointment.TimeZoneId = dto.TimeZoneId;

            if (dto.IsRecurring.HasValue) appointment.IsRecurring = dto.IsRecurring.Value;
            if (dto.RecurrenceRule != null) appointment.RecurrenceRule = dto.RecurrenceRule;
            if (dto.RecurrenceEnd.HasValue) appointment.RecurrenceEnd = dto.RecurrenceEnd;
            if (dto.OccurrenceCount.HasValue) appointment.OccurrenceCount = dto.OccurrenceCount;

            _unitOfWork.Appointments.Update(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }


        public async Task<bool> Delete(int id)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para excluir agendamentos.");


            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

            _unitOfWork.Appointments.Delete(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }
    }

    public interface IAppointmentService
    {
        Task<PagedResult<Appointment>> GetPagedAppointments(AppointmentFiltersDTO filters);
        Task<List<Appointment>> GetByCompany(int companyId);
        Task<List<Appointment>> GetByTeam(int teamId);
        Task<List<Appointment>> GetByProfessional(int professionalId);
        Task<List<Appointment>> GetByCustomer(int customerId);
        Task<List<Appointment>> GetByDateRange(DateTime start, DateTime end, int? companyId = null);
        Task<Appointment?> GetById(int id);
        Task<bool> Create(CreateAppointmentDTO dto);
        Task<bool> Update(int id, UpdateAppointmentDTO dto);
        Task<bool> Delete(int id);
    }
}