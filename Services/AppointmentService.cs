using Core.DTO.Appointment;
using Core.Enums.Appointment;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
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
        private readonly DbContextClass _db;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IAppointmentCompletionService _completion;

        public AppointmentService(
            Infrastructure.Repositories.IUnitOfWork unitOfWork,
            DbContextClass db,
            ICurrentUser currentUser,
            IScopeGuard scope,
            IAppointmentCompletionService completion)
        {
            _unitOfWork = unitOfWork;
            _db = db;
            _currentUser = currentUser;
            _scope = scope;
            _completion = completion;
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

            var category = dto.Category;
            if (string.IsNullOrWhiteSpace(category) && dto.Type.HasValue)
                category = dto.Type.Value.ToString();

            var appointment = new Appointment
            {
                Title = dto.Title,
                Address = dto.Address,
                Start = dto.Start,
                End = dto.End,
                Notes = dto.Notes,
                Status = dto.Status ?? AppointmentStatus.Scheduled,
                Type = dto.Type ?? AppointmentType.Regular,
                Category = category,
                ServiceTypeId = dto.ServiceTypeId,
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

            await ValidateServiceTypeForCompanyAsync(appointment.CompanyId, appointment.ServiceTypeId);

            await _unitOfWork.Appointments.Add(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }

        public async Task<bool> Update(int id, UpdateAppointmentDTO dto)
        {
            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            var oldStatus = appointment.Status;

            // Admin bypass; demais perfis ficam restritos ao escopo
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

            // Profissional pode atualizar SOMENTE campos de check-in/execução (ex.: status/notes)
            // desde que o agendamento pertença ao seu escopo (profissional ou membership de team).
            if (_currentUser.IsProfessional)
            {
                await _scope.EnsureAppointmentAccessAsync(id);

                EnsureProfessionalUpdateIsSafe(appointment, dto);

                if (dto.Status.HasValue)
                    appointment.Status = dto.Status.Value;

                if (dto.Notes != null)
                    appointment.Notes = dto.Notes;

                if (oldStatus != AppointmentStatus.Completed && appointment.Status == AppointmentStatus.Completed)
                {
                    await _completion.RecordCompletionAsync(appointment, appointment.Start, appointment.End);
                }

                _unitOfWork.Appointments.Update(appointment);
                return await _unitOfWork.SaveAsync() > 0;
            }

            // Company (e demais perfis internos) mantém o comportamento atual de edição
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
            // Category/ServiceType (Payroll)
            if (dto.Category != null) appointment.Category = dto.Category;
            if (dto.ServiceTypeId.HasValue) appointment.ServiceTypeId = dto.ServiceTypeId;
            _ = dto.CompanyId; // CompanyId não é alterável aqui
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

            // Sempre que o status transicionar para Completed, registra um snapshot em AppointmentCompletions
            // (sem duplicar registros; o service já faz dedupe por AppointmentId + OccurrenceStart).
            if (oldStatus != AppointmentStatus.Completed && appointment.Status == AppointmentStatus.Completed)
            {
                await _completion.RecordCompletionAsync(appointment, appointment.Start, appointment.End);
            }

            _unitOfWork.Appointments.Update(appointment);
            return await _unitOfWork.SaveAsync() > 0;
        }


        private async Task ValidateServiceTypeForCompanyAsync(int companyId, int? serviceTypeId)
        {
            if (!serviceTypeId.HasValue) return;

            var st = await _unitOfWork.ServiceTypes.GetById(serviceTypeId.Value);
            if (st == null)
                throw new BadRequestException("ServiceTypeId inválido.");

            if (st.CompanyId != companyId)
                throw new ForbiddenException("ServiceType não pertence a esta company.");
        }

        private static void EnsureProfessionalUpdateIsSafe(Appointment appointment, UpdateAppointmentDTO dto)
        {
            // Permitimos que o front envie o objeto inteiro, mas o profissional só pode
            // efetivamente ALTERAR Status/Notes. Qualquer outro campo diferente do existente
            // é bloqueado para evitar "edição" indevida do agendamento.

            if (dto.Scope != RecurrenceScope.This)
                throw new ForbiddenException("Profissional não tem permissão para editar recorrência.");

            if (dto.OccurrenceStart.HasValue || dto.OccurrenceEnd.HasValue)
                throw new ForbiddenException("Profissional não tem permissão para editar ocorrências de recorrência.");

            if (dto.Title != null && dto.Title != appointment.Title)
                throw new ForbiddenException("Profissional não tem permissão para alterar título do agendamento.");

            if (dto.Address != null && dto.Address != appointment.Address)
                throw new ForbiddenException("Profissional não tem permissão para alterar endereço do agendamento.");

            if (dto.Start.HasValue && dto.Start.Value != appointment.Start)
                throw new ForbiddenException("Profissional não tem permissão para alterar horário do agendamento.");

            if (dto.End.HasValue && dto.End.Value != appointment.End)
                throw new ForbiddenException("Profissional não tem permissão para alterar horário do agendamento.");

            if (dto.CompanyId.HasValue && dto.CompanyId.Value != appointment.CompanyId)
                throw new ForbiddenException("Profissional não tem permissão para alterar company do agendamento.");

            if (dto.CustomerId.HasValue && dto.CustomerId.Value != appointment.CustomerId)
                throw new ForbiddenException("Profissional não tem permissão para alterar cliente do agendamento.");

            if (dto.TeamId.HasValue && dto.TeamId.Value != appointment.TeamId)
                throw new ForbiddenException("Profissional não tem permissão para alterar equipe do agendamento.");

            if (dto.Type.HasValue && dto.Type.Value != appointment.Type)
                throw new ForbiddenException("Profissional não tem permissão para alterar tipo do agendamento.");

            if (dto.Category != null && dto.Category != appointment.Category)
                throw new ForbiddenException("Profissional não tem permissão para alterar category do agendamento.");

            if (dto.ServiceTypeId.HasValue && dto.ServiceTypeId.Value != appointment.ServiceTypeId)
                throw new ForbiddenException("Profissional não tem permissão para alterar service type do agendamento.");

            if (!string.IsNullOrWhiteSpace(dto.TimeZoneId) && dto.TimeZoneId != appointment.TimeZoneId)
                throw new ForbiddenException("Profissional não tem permissão para alterar fuso do agendamento.");

            if (dto.IsRecurring.HasValue && dto.IsRecurring.Value != appointment.IsRecurring)
                throw new ForbiddenException("Profissional não tem permissão para alterar recorrência do agendamento.");

            if (dto.RecurrenceRule != null && dto.RecurrenceRule != appointment.RecurrenceRule)
                throw new ForbiddenException("Profissional não tem permissão para alterar recorrência do agendamento.");

            if (dto.RecurrenceEnd.HasValue && dto.RecurrenceEnd.Value != appointment.RecurrenceEnd)
                throw new ForbiddenException("Profissional não tem permissão para alterar recorrência do agendamento.");

            if (dto.OccurrenceCount.HasValue && dto.OccurrenceCount.Value != appointment.OccurrenceCount)
                throw new ForbiddenException("Profissional não tem permissão para alterar recorrência do agendamento.");

            if (dto.ProfessionalIds != null)
            {
                var existing = (appointment.ProfessionalIds ?? new List<int>()).Distinct().OrderBy(x => x).ToList();
                var incoming = dto.ProfessionalIds.Distinct().OrderBy(x => x).ToList();
                if (existing.Count != incoming.Count || !existing.SequenceEqual(incoming))
                    throw new ForbiddenException("Profissional não tem permissão para alterar profissionais do agendamento.");
            }
        }


        public async Task<bool> Delete(int id)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para excluir agendamentos.");


            var appointment = await _unitOfWork.Appointments.GetById(id);
            if (appointment == null) return false;

            var oldStatus = appointment.Status;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

            // Limpa registros dependentes antes de apagar o Appointment.
            // Motivação: FK PayrollItems.AppointmentId e AppointmentCompletions.AppointmentId estão com DeleteBehavior.Restrict
            // e estouram erro 23503 quando o appointment já foi marcado como completed.
            var payrollItems = await _db.PayrollItems
                .Where(i => i.AppointmentId == id)
                .ToListAsync();
            if (payrollItems.Count > 0)
                _db.PayrollItems.RemoveRange(payrollItems);

            var completions = await _db.AppointmentCompletions
                .Where(c => c.AppointmentId == id)
                .ToListAsync();
            if (completions.Count > 0)
                _db.AppointmentCompletions.RemoveRange(completions);

            // Series cleanup: when deleting a recurring appointment anchor, also delete all
            // recurrence exception records linked to its SeriesId.
            if (appointment.IsRecurring && appointment.SeriesId.HasValue)
            {
                var seriesId = appointment.SeriesId.Value;
                var recurrenceExceptions = await _db.AppointmentRecurrenceExceptions
                    .Where(e => e.SeriesId == seriesId)
                    .ToListAsync();

                if (recurrenceExceptions.Count > 0)
                    _db.AppointmentRecurrenceExceptions.RemoveRange(recurrenceExceptions);
            }

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