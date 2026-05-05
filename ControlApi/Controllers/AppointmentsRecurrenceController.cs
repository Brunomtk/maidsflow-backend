using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Appointment;
using Core.Exceptions;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Integrations.Twilio;
using Services.Localization;
using Services.Security;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentsRecurrenceController : ControllerBase
    {
        private readonly DbContextClass _db;
        private readonly ITwilioSmsSender _sms;
        private readonly ICurrentUser _currentUser;
        private readonly IMessageLocalizer _loc;
        private readonly IRecipientLanguageResolver _langResolver;

        public AppointmentsRecurrenceController(
            DbContextClass db,
            ITwilioSmsSender sms,
            ICurrentUser currentUser,
            IMessageLocalizer loc,
            IRecipientLanguageResolver langResolver)
        {
            _db = db;
            _sms = sms;
            _currentUser = currentUser;
            _loc = loc;
            _langResolver = langResolver;
        }

        private static string BuildCustomerAddressLine(CustomerAddress addr)
        {
            var parts = new List<string>();
            var line1 = addr.AddressLine1?.Trim();
            var line2 = addr.AddressLine2?.Trim();
            if (!string.IsNullOrWhiteSpace(line1)) parts.Add(line1!);
            if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2!);

            var cityState = string.Join(", ", new[] { addr.City?.Trim(), addr.State?.Trim() }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(cityState)) parts.Add(cityState);

            var zip = addr.ZipCode?.Trim();
            if (!string.IsNullOrWhiteSpace(zip)) parts.Add(zip!);

            return string.Join(" - ", parts);
        }

        // CREATE (single or recurring)
                [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentDTO dto)
        {
            var tz = ResolveTimeZone(dto.TimeZoneId);

            // Sempre trabalhamos com horários locais (sem conversão para UTC aqui).
            var startLocal = dto.Start;
            var endLocal   = dto.End;

            // Agendamento NÃO recorrente: mantém o comportamento original.
            if (!dto.IsRecurring)
            {
                var appointment = MapAppointment(dto, startLocal, endLocal, tz, false, null);
                await _db.Set<Appointment>().AddAsync(appointment);
                await _db.SaveChangesAsync();
                return Ok(appointment);
            }

            // Agendamento recorrente:
            // A partir de agora vamos persistir apenas UM registro na tabela de Appointments,
            // carregando a regra de recorrência (RecurrenceRule / RecurrenceEnd / OccurrenceCount).
            // A expansão em múltiplas ocorrências fica para a camada de leitura.
            if (string.IsNullOrWhiteSpace(dto.RecurrenceRule))
            {
                return BadRequest("RecurrenceRule é obrigatório para agendamentos recorrentes.");
            }

            var seriesId = Guid.NewGuid();
            var recurringAppointment = MapAppointment(dto, startLocal, endLocal, tz, true, seriesId);

            await _db.Set<Appointment>().AddAsync(recurringAppointment);
            await _db.SaveChangesAsync();

            return Ok(recurringAppointment);
        }
// UPDATE with scope
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDTO dto)
        {
            var current = await _db.Set<Appointment>().FindAsync(id);
            if (current == null) return NotFound();

            var oldAnchorStatus = current.Status;

            var tz = ResolveTimeZone(dto.TimeZoneId ?? current.TimeZoneId);

            // Non-recurring (or no SeriesId) keeps the classic behavior
            if (!current.IsRecurring || current.SeriesId == null)
            {
                await UpdateThisAsync(current, dto, tz);

                // Se o status mudou para Completed, registra snapshot em AppointmentCompletions.
                if (oldAnchorStatus != Core.Enums.Appointment.AppointmentStatus.Completed &&
                    current.Status == Core.Enums.Appointment.AppointmentStatus.Completed)
                {
                    await RecordCompletionSnapshotIfNeededAsync(current, current.Start, current.End, dto.ProfessionalIds);
                }

                await _db.SaveChangesAsync();
                return Ok(current);
            }

            // Recurring series: we persist ONLY ONE anchor row in Appointments,
            // and store per-occurrence edits/deletes as exceptions.
            if (dto.Scope == RecurrenceScope.This)
            {
                if (!dto.OccurrenceStart.HasValue)
                    return BadRequest("OccurrenceStart é obrigatório para Scope=This em séries recorrentes.");

                await UpsertExceptionOverrideAsync(current, dto, tz);

                // Quando marcar uma ocorrência como Completed, grava o snapshot (dedupe por AppointmentId + OccurrenceStart)
                if (dto.Status.HasValue && dto.Status.Value == Core.Enums.Appointment.AppointmentStatus.Completed)
                {
                    var (ws, we) = ResolveOccurrenceWindow(current, dto.OccurrenceStart.Value, dto.OccurrenceEnd);
                    await RecordCompletionSnapshotIfNeededAsync(current, ws, we, dto.ProfessionalIds);
                }

                await _db.SaveChangesAsync();
                return Ok(current);
            }

            if (dto.Scope == RecurrenceScope.ThisAndFollowing)
            {
                if (!dto.OccurrenceStart.HasValue)
                    return BadRequest("OccurrenceStart é obrigatório para Scope=ThisAndFollowing em séries recorrentes.");

                await UpdateThisAndFollowingAsync(current, dto, tz);

                // Se o status da série (âncora) mudou para Completed, registra snapshot para a ocorrência âncora.
                if (oldAnchorStatus != Core.Enums.Appointment.AppointmentStatus.Completed &&
                    current.Status == Core.Enums.Appointment.AppointmentStatus.Completed)
                {
                    await RecordCompletionSnapshotIfNeededAsync(current, current.Start, current.End, dto.ProfessionalIds);
                }

                await _db.SaveChangesAsync();
                return Ok(current);
            }

            if (dto.Scope == RecurrenceScope.All)
            {
                await UpdateAllAsync(current, dto, tz);

                if (oldAnchorStatus != Core.Enums.Appointment.AppointmentStatus.Completed &&
                    current.Status == Core.Enums.Appointment.AppointmentStatus.Completed)
                {
                    await RecordCompletionSnapshotIfNeededAsync(current, current.Start, current.End, dto.ProfessionalIds);
                }

                await _db.SaveChangesAsync();
                return Ok(current);
            }

            return BadRequest("Invalid scope.");
        }

        // DELETE with scope
        
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(
            int id,
            [FromQuery] RecurrenceScope scope = RecurrenceScope.This,
            [FromQuery] DateTime? occurrenceStart = null,
            [FromQuery] DateTime? occurrenceEnd = null)
        {
            var current = await _db.Set<Appointment>().FindAsync(id);
            if (current == null) return NotFound();

            // Non-recurring: classic delete
            if (!current.IsRecurring || current.SeriesId == null)
            {
                // Se o agendamento já foi marcado como completed, podem existir PayrollItems/Completions
                // apontando para ele (FK Restrict). Precisamos limpar antes de remover o Appointment.
                await CleanupAppointmentReferencesAsync(current.Id);
                _db.Set<Appointment>().Remove(current);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            var seriesId = current.SeriesId.Value;

            if (scope == RecurrenceScope.All)
            {
                var exAll = await _db.Set<AppointmentRecurrenceException>()
                    .Where(e => e.SeriesId == seriesId)
                    .ToListAsync();

                _db.Set<AppointmentRecurrenceException>().RemoveRange(exAll);

                // Limpa referências (PayrollItems/Completions) da âncora antes de apagar.
                await CleanupAppointmentReferencesAsync(current.Id);
                _db.Set<Appointment>().Remove(current);

                await _db.SaveChangesAsync();
                return NoContent();
            }

            if (!occurrenceStart.HasValue)
                return BadRequest("occurrenceStart é obrigatório para scope This ou ThisAndFollowing em séries recorrentes.");

            if (scope == RecurrenceScope.This)
            {
                // Se essa ocorrência já foi concluída (ou já entrou no payroll), removemos os registros
                // dependentes antes de “cancelar” a instância via exception. Assim evitamos ficar com
                // snapshots/origens órfãs em AppointmentCompletions e PayrollItems.
                await CleanupAppointmentOccurrenceReferencesAsync(current.Id, occurrenceStart.Value);
                await UpsertExceptionCancellationAsync(current, occurrenceStart.Value, occurrenceEnd);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            if (scope == RecurrenceScope.ThisAndFollowing)
            {
                // Ao cortar/deletar “esta e as seguintes”, removemos qualquer completion/payroll já gerado
                // a partir do corte, para não manter histórico em instâncias que deixam de existir.
                await CleanupAppointmentOccurrenceReferencesFromAsync(current.Id, occurrenceStart.Value);
                await CutSeriesAsync(current, occurrenceStart.Value);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            return BadRequest("Invalid scope.");
        }


// GET series
        [HttpGet("series/{seriesId:guid}")]
        public async Task<IActionResult> GetSeries(Guid seriesId)
        {
            var list = await _db.Set<Appointment>()
                .Where(a => a.SeriesId == seriesId)
                .OrderBy(a => a.Start)
                .ToListAsync();
            return Ok(list);
        }

        

        [HttpGet("series/{seriesId:guid}/exceptions")]
        public async Task<IActionResult> GetSeriesExceptions(Guid seriesId)
        {
            var exceptions = await _db.Set<AppointmentRecurrenceException>()
                .Where(e => e.SeriesId == seriesId)
                .OrderBy(e => e.OccurrenceStart)
                .ToListAsync();

            return Ok(exceptions);
        }


/// <summary>
/// Endpoint de leitura para calendário: retorna eventos normais + ocorrências recorrentes EXPANDIDAS
/// no intervalo informado, já com exceções (edit/cancel) aplicadas.
///
/// - Eventos normais retornam AppointmentId
/// - Ocorrências recorrentes retornam InstanceId (rec_{seriesIdN}_{ticks})
/// </summary>
[HttpGet("calendar")]
public async Task<IActionResult> GetCalendar(
    // Aceita tanto ?start quanto ?Start (binder é case-insensitive, mas deixamos explícito)
    [FromQuery(Name = "Start")] DateTime start,
    [FromQuery(Name = "End")] DateTime end,
    [FromQuery] int? companyId = null,
    [FromQuery] int? teamId = null,
    [FromQuery] int? customerId = null,
    // Filtro do calendário do PROFESSIONAL
    [FromQuery(Name = "ProfessionalId")] int? professionalId = null)
{
    if (end <= start) return BadRequest("end deve ser maior que start.");

    var rangeStart = start;
    var rangeEnd = end;

    // 1) Eventos não recorrentes (normais)
    var normalQuery = _db.Set<Appointment>().AsNoTracking()
        .Include(a => a.Company)
        .Include(a => a.Customer)
        .Include(a => a.CustomerAddress)
        .Include(a => a.Team)
        .Include(a => a.ServiceType)
        .Where(a => !a.IsRecurring && a.Start < rangeEnd && a.End > rangeStart);

    if (companyId.HasValue) normalQuery = normalQuery.Where(a => a.CompanyId == companyId.Value);
    if (teamId.HasValue) normalQuery = normalQuery.Where(a => a.TeamId == teamId.Value);
    if (customerId.HasValue) normalQuery = normalQuery.Where(a => a.CustomerId == customerId.Value);

    var normals = await normalQuery.ToListAsync();

    // Filtro por ProfessionalId (view do professional):
    // Appointment.ProfessionalIds é NotMapped, então filtramos em memória.
    if (professionalId.HasValue)
    {
        var pid = professionalId.Value;
        normals = normals
            .Where(a => a.ProfessionalIds != null && a.ProfessionalIds.Contains(pid))
            .ToList();
    }

    // 2) Âncoras recorrentes
    var anchorsQuery = _db.Set<Appointment>().AsNoTracking()
        .Include(a => a.Company)
        .Include(a => a.Customer)
        .Include(a => a.CustomerAddress)
        .Include(a => a.Team)
        .Include(a => a.ServiceType)
        .Where(a => a.IsRecurring
                 && a.SeriesId != null
                 && !string.IsNullOrWhiteSpace(a.RecurrenceRule)
                 && a.Start <= rangeEnd
                 && (!a.RecurrenceEnd.HasValue || a.RecurrenceEnd.Value >= rangeStart));

    if (companyId.HasValue) anchorsQuery = anchorsQuery.Where(a => a.CompanyId == companyId.Value);
    if (teamId.HasValue) anchorsQuery = anchorsQuery.Where(a => a.TeamId == teamId.Value);
    if (customerId.HasValue) anchorsQuery = anchorsQuery.Where(a => a.CustomerId == customerId.Value);

    var anchors = await anchorsQuery.ToListAsync();

    // Se for calendário do professional, reduzimos o trabalho: só séries que tenham o ProfessionalId
    // OU que tenham exceção com overrideProfessionalIds contendo o ProfessionalId (tratado mais abaixo).
    // Aqui fazemos um filtro inicial pelo "base".
    if (professionalId.HasValue)
    {
        var pid = professionalId.Value;
        anchors = anchors
            .Where(a => a.ProfessionalIds != null && a.ProfessionalIds.Contains(pid))
            .ToList();
    }
    var seriesIds = anchors.Select(a => a.SeriesId!.Value).Distinct().ToList();

    // 3) Exceções (somente do intervalo — com buffer pra não perder overrides próximos)
    var exStart = rangeStart.AddDays(-7);
    var exEnd = rangeEnd.AddDays(7);

    var exceptions = await _db.Set<AppointmentRecurrenceException>().AsNoTracking()
        .Where(e => seriesIds.Contains(e.SeriesId)
                 && e.OccurrenceStart <= exEnd
                 && e.OccurrenceEnd >= exStart)
        .OrderBy(e => e.SeriesId)
        .ThenBy(e => e.OccurrenceStart)
        .ToListAsync();

    var exMap = exceptions
        .GroupBy(e => (e.SeriesId, e.OccurrenceStart))
        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedDate).First());

    // ServiceType lookup (needed because recurring overrides may change ServiceTypeId
    // and we don't want extra per-occurrence queries).
    var serviceTypeIds = new HashSet<int>();
    foreach (var a in normals)
        if (a.ServiceTypeId.HasValue) serviceTypeIds.Add(a.ServiceTypeId.Value);
    foreach (var a in anchors)
        if (a.ServiceTypeId.HasValue) serviceTypeIds.Add(a.ServiceTypeId.Value);
    foreach (var e in exceptions)
        if (e.OverrideServiceTypeId.HasValue) serviceTypeIds.Add(e.OverrideServiceTypeId.Value);

    var serviceTypeNameMap = serviceTypeIds.Count == 0
        ? new Dictionary<int, string>()
        : await _db.Set<ServiceType>().AsNoTracking()
            .Where(s => serviceTypeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name);

    // CustomerAddress lookup (evita N+1 e evita que o front tenha que chamar outros endpoints)
    var customerAddressIds = new HashSet<int>();
    foreach (var a in normals)
        if (a.CustomerAddressId.HasValue) customerAddressIds.Add(a.CustomerAddressId.Value);
    foreach (var a in anchors)
        if (a.CustomerAddressId.HasValue) customerAddressIds.Add(a.CustomerAddressId.Value);
    foreach (var e in exceptions)
        if (e.OverrideCustomerAddressId.HasValue) customerAddressIds.Add(e.OverrideCustomerAddressId.Value);

    var customerAddressMap = customerAddressIds.Count == 0
        ? new Dictionary<int, CustomerAddress>()
        : await _db.Set<CustomerAddress>().AsNoTracking()
            .Where(ca => customerAddressIds.Contains(ca.Id))
            .ToDictionaryAsync(ca => ca.Id, ca => ca);

    string ResolveAddressString(
        string? overrideAddress,
        int? finalCustomerAddressId,
        string? snapshotAddress,
        CustomerAddress? navCustomerAddress,
        string? legacyCustomerAddress)
    {
        if (!string.IsNullOrWhiteSpace(overrideAddress))
            return overrideAddress!;

        if (finalCustomerAddressId.HasValue && customerAddressMap.TryGetValue(finalCustomerAddressId.Value, out var addr))
            return BuildCustomerAddressLine(addr);

        if (!string.IsNullOrWhiteSpace(snapshotAddress))
            return snapshotAddress!;

        if (navCustomerAddress != null && !string.IsNullOrWhiteSpace(navCustomerAddress.AddressLine1))
            return BuildCustomerAddressLine(navCustomerAddress);

        return legacyCustomerAddress ?? string.Empty;
    }

    var outList = new List<CalendarOccurrenceDTO>();

    // Normal -> Calendar DTO
    foreach (var a in normals)
    {
        var finalCustomerAddressId = a.CustomerAddressId;
        var resolvedAddress = ResolveAddressString(
            overrideAddress: null,
            finalCustomerAddressId: finalCustomerAddressId,
            snapshotAddress: a.Address,
            navCustomerAddress: a.CustomerAddress,
            legacyCustomerAddress: a.Customer?.Address);

        var resolvedCustomerPhone = !string.IsNullOrWhiteSpace(a.CustomerAddress?.Phone) ? a.CustomerAddress.Phone : a.Customer?.Phone;
        var resolvedCustomerPhone2 = !string.IsNullOrWhiteSpace(a.CustomerAddress?.Phone2) ? a.CustomerAddress.Phone2 : a.Customer?.Phone2;

        var customerMini = a.Customer != null
            ? new CalendarCustomerMiniDTO
            {
                Id = a.Customer.Id,
                Name = a.Customer.Name,
                Email = a.Customer.Email,
                Phone = resolvedCustomerPhone,
                Phone2 = resolvedCustomerPhone2,
                AddressPhone = a.CustomerAddress?.Phone,
                AddressPhone2 = a.CustomerAddress?.Phone2,
                Address = string.IsNullOrWhiteSpace(resolvedAddress) ? a.Customer.Address : resolvedAddress,
                ReceiveSms = a.Customer.ReceiveSms,
                ReceiveEmail = a.Customer.ReceiveEmail,
            }
            : null;
        var teamMini = a.Team != null
            ? new CalendarTeamMiniDTO { Id = a.Team.Id, Name = a.Team.Name, Color = a.Team.Color }
            : null;

        var title = !string.IsNullOrWhiteSpace(a.Title)
            ? a.Title
            : (customerMini?.Name ?? "No Customer");

        outList.Add(new CalendarOccurrenceDTO
        {
            Id = a.Id.ToString(),
            AppointmentId = a.Id,
            IsVirtualOccurrence = false,
            IsRecurring = false,

            Start = a.Start,
            End = a.End,
            Title = title,
            Address = resolvedAddress,
            Notes = a.Notes,

            CustomerEmail = a.Customer?.Email,
            CustomerPhone = resolvedCustomerPhone,
            CustomerPhone2 = resolvedCustomerPhone2,
            CustomerAddressPhone = a.CustomerAddress?.Phone,
            CustomerAddressPhone2 = a.CustomerAddress?.Phone2,
            CustomerAddress = string.IsNullOrWhiteSpace(resolvedAddress) ? a.Customer?.Address : resolvedAddress,

            CompanyName = a.Company?.Name,

            CompanyReceiveSms = a.Company?.ReceiveSms,
            CompanyReceiveEmail = a.Company?.ReceiveEmail,
            CustomerReceiveSms = a.Customer?.ReceiveSms,
            CustomerReceiveEmail = a.Customer?.ReceiveEmail,

            CompanyId = a.CompanyId,
            CustomerId = a.CustomerId,
            CustomerAddressId = finalCustomerAddressId,
            TeamId = a.TeamId,
            Status = a.Status,
            Type = a.Type,
            Category = a.Category ?? a.Type.ToString(),
            ServiceTypeId = a.ServiceTypeId,
            ServiceTypeName = a.ServiceType?.Name
                ?? (a.ServiceTypeId.HasValue && serviceTypeNameMap.TryGetValue(a.ServiceTypeId.Value, out var stNameA)
                    ? stNameA
                    : null),
            Customer = customerMini,
            Team = teamMini,
            ProfessionalIds = a.ProfessionalIds?.ToList() ?? new List<int>(),
            ProfessionalId = (a.ProfessionalIds != null && a.ProfessionalIds.Any()) ? a.ProfessionalIds.First() : null
        });
    }

    // Recorrentes -> expand + apply exceptions
    foreach (var anchor in anchors)
    {
        var customerBase = anchor.Customer;

        var teamMini = anchor.Team != null
            ? new CalendarTeamMiniDTO { Id = anchor.Team.Id, Name = anchor.Team.Name, Color = anchor.Team.Color }
            : null;

        CalendarCustomerMiniDTO? MakeCustomerMini(string resolvedAddress)
        {
            if (customerBase == null) return null;

            return new CalendarCustomerMiniDTO
            {
                Id = customerBase.Id,
                Name = customerBase.Name,
                Email = customerBase.Email,
                Phone = !string.IsNullOrWhiteSpace(anchor.CustomerAddress?.Phone) ? anchor.CustomerAddress.Phone : customerBase.Phone,
                Phone2 = !string.IsNullOrWhiteSpace(anchor.CustomerAddress?.Phone2) ? anchor.CustomerAddress.Phone2 : customerBase.Phone2,
                AddressPhone = anchor.CustomerAddress?.Phone,
                AddressPhone2 = anchor.CustomerAddress?.Phone2,
                Address = string.IsNullOrWhiteSpace(resolvedAddress) ? customerBase.Address : resolvedAddress,
                ReceiveSms = customerBase.ReceiveSms,
                ReceiveEmail = customerBase.ReceiveEmail,
            };
        }

        var anchorFinalCustomerAddressId = anchor.CustomerAddressId;
        var anchorResolvedAddress = ResolveAddressString(
            overrideAddress: null,
            finalCustomerAddressId: anchorFinalCustomerAddressId,
            snapshotAddress: anchor.Address,
            navCustomerAddress: anchor.CustomerAddress,
            legacyCustomerAddress: customerBase?.Address);

        var tz = ResolveTimeZone(anchor.TimeZoneId);

        // Limita geração ao rangeEnd (ou RecurrenceEnd, se menor)
        DateTime? seriesEnd = anchor.RecurrenceEnd.HasValue
            ? (anchor.RecurrenceEnd.Value < rangeEnd ? anchor.RecurrenceEnd.Value : rangeEnd)
            : rangeEnd;

        var occs = ExpandOccurrences(
            anchor.RecurrenceRule!,
            anchor.Start,
            anchor.End,
            seriesEnd,
            anchor.OccurrenceCount,
            tz);

        foreach (var (occStart, occEnd) in occs)
        {
            // filtra por interseção com o range
            if (occStart >= rangeEnd || occEnd <= rangeStart) continue;

            var key = (anchor.SeriesId!.Value, occStart);
            if (exMap.TryGetValue(key, out var ex))
            {
                if (ex.IsCancelled)
                    continue; // cancelado -> não aparece no calendário

                var startFinal = ex.OverrideStart ?? occStart;
                var endFinal = ex.OverrideEnd ?? occEnd;

                // após override, ainda precisa intersectar o range
                if (startFinal >= rangeEnd || endFinal <= rangeStart) continue;

                var instId = EncodeInstanceId(anchor.SeriesId!.Value, occStart);

                var title = !string.IsNullOrWhiteSpace(ex.OverrideTitle)
                    ? ex.OverrideTitle
                    : (!string.IsNullOrWhiteSpace(anchor.Title)
                        ? anchor.Title
                        : (customerBase?.Name ?? "No Customer"));

                // ProfessionalIds: só usa override se tiver pelo menos 1 id, senão mantém o da âncora
                var finalProfessionalIds = (ex.OverrideProfessionalIds != null && ex.OverrideProfessionalIds.Any())
                    ? ex.OverrideProfessionalIds.Distinct().ToList()
                    : anchor.ProfessionalIds?.Distinct().ToList() ?? new List<int>();

                // Aplica filtro do ProfessionalId após o merge (override pode mudar profissionais)
                if (professionalId.HasValue && !finalProfessionalIds.Contains(professionalId.Value))
                    continue;

                var finalCustomerAddressId = ex.OverrideCustomerAddressId ?? anchor.CustomerAddressId;
                var resolvedAddress = ResolveAddressString(
                    overrideAddress: ex.OverrideAddress,
                    finalCustomerAddressId: finalCustomerAddressId,
                    snapshotAddress: anchor.Address,
                    navCustomerAddress: anchor.CustomerAddress,
                    legacyCustomerAddress: anchor.Customer?.Address);

                outList.Add(new CalendarOccurrenceDTO
                {
                    Id = instId,
                    InstanceId = instId,
                    IsVirtualOccurrence = true,
                    IsRecurring = true,
                    AppointmentId = anchor.Id,
                    AnchorAppointmentId = anchor.Id,
                    SeriesId = anchor.SeriesId,

                    Start = startFinal,
                    End = endFinal,
                    Title = title,
                    Address = resolvedAddress,
                    Notes = ex.OverrideNotes ?? anchor.Notes,

                    CustomerEmail = anchor.Customer?.Email,
                    CustomerPhone = !string.IsNullOrWhiteSpace(anchor.CustomerAddress?.Phone) ? anchor.CustomerAddress.Phone : anchor.Customer?.Phone,
                    CustomerPhone2 = !string.IsNullOrWhiteSpace(anchor.CustomerAddress?.Phone2) ? anchor.CustomerAddress.Phone2 : anchor.Customer?.Phone2,
                    CustomerAddressPhone = anchor.CustomerAddress?.Phone,
                    CustomerAddressPhone2 = anchor.CustomerAddress?.Phone2,
                    CustomerAddress = string.IsNullOrWhiteSpace(resolvedAddress) ? anchor.Customer?.Address : resolvedAddress,

                    CompanyName = anchor.Company?.Name,

                    CompanyReceiveSms = anchor.Company?.ReceiveSms,
                    CompanyReceiveEmail = anchor.Company?.ReceiveEmail,
                    CustomerReceiveSms = anchor.Customer?.ReceiveSms,
                    CustomerReceiveEmail = anchor.Customer?.ReceiveEmail,

                    CompanyId = anchor.CompanyId,
                    CustomerId = anchor.CustomerId,
                    CustomerAddressId = finalCustomerAddressId,
                    TeamId = anchor.TeamId,
                    Status = ex.OverrideStatus ?? anchor.Status,
                    Type = ex.OverrideType ?? anchor.Type,
                    Category = (ex.OverrideType ?? anchor.Type).ToString(),
                    ServiceTypeId = ex.OverrideServiceTypeId ?? anchor.ServiceTypeId,
                    ServiceTypeName = (ex.OverrideServiceTypeId ?? anchor.ServiceTypeId).HasValue
                        ? serviceTypeNameMap.GetValueOrDefault((ex.OverrideServiceTypeId ?? anchor.ServiceTypeId)!.Value)
                        : null,

                    Customer = MakeCustomerMini(resolvedAddress),
                    Team = teamMini,

                    ProfessionalIds = finalProfessionalIds,

                    ProfessionalId = finalProfessionalIds.Any() ? finalProfessionalIds.First() : null,

                    HasOverride = true
                });

                continue;
            }

            // sem exceção
            var instanceId = EncodeInstanceId(anchor.SeriesId!.Value, occStart);

            var baseTitle = !string.IsNullOrWhiteSpace(anchor.Title)
                ? anchor.Title
                : (customerBase?.Name ?? "No Customer");

            var baseProfessionalIds = anchor.ProfessionalIds?.Distinct().ToList() ?? new List<int>();
            if (professionalId.HasValue && !baseProfessionalIds.Contains(professionalId.Value))
                continue;

            var resolvedAddressNoEx = ResolveAddressString(
                overrideAddress: null,
                finalCustomerAddressId: anchor.CustomerAddressId,
                snapshotAddress: anchor.Address,
                navCustomerAddress: anchor.CustomerAddress,
                legacyCustomerAddress: anchor.Customer?.Address);

            outList.Add(new CalendarOccurrenceDTO
            {
                Id = instanceId,
                InstanceId = instanceId,
                IsVirtualOccurrence = true,
                IsRecurring = true,
                AppointmentId = anchor.Id,
                AnchorAppointmentId = anchor.Id,
                SeriesId = anchor.SeriesId,

                Start = occStart,
                End = occEnd,

                Title = baseTitle,
                Address = resolvedAddressNoEx,
                Notes = anchor.Notes,

                CustomerEmail = anchor.Customer?.Email,
                CustomerPhone = !string.IsNullOrWhiteSpace(anchor.CustomerAddress?.Phone) ? anchor.CustomerAddress.Phone : anchor.Customer?.Phone,
                    CustomerPhone2 = !string.IsNullOrWhiteSpace(anchor.CustomerAddress?.Phone2) ? anchor.CustomerAddress.Phone2 : anchor.Customer?.Phone2,
                    CustomerAddressPhone = anchor.CustomerAddress?.Phone,
                    CustomerAddressPhone2 = anchor.CustomerAddress?.Phone2,
                CustomerAddress = string.IsNullOrWhiteSpace(resolvedAddressNoEx) ? anchor.Customer?.Address : resolvedAddressNoEx,

                CompanyName = anchor.Company?.Name,

                CompanyReceiveSms = anchor.Company?.ReceiveSms,
                CompanyReceiveEmail = anchor.Company?.ReceiveEmail,
                CustomerReceiveSms = anchor.Customer?.ReceiveSms,
                CustomerReceiveEmail = anchor.Customer?.ReceiveEmail,

                CompanyId = anchor.CompanyId,
                CustomerId = anchor.CustomerId,
                CustomerAddressId = anchor.CustomerAddressId,
                TeamId = anchor.TeamId,
                Status = anchor.Status,
                Type = anchor.Type,
                Category = anchor.Category ?? anchor.Type.ToString(),
                ServiceTypeId = anchor.ServiceTypeId,
                ServiceTypeName = anchor.ServiceTypeId.HasValue
                    ? serviceTypeNameMap.GetValueOrDefault(anchor.ServiceTypeId.Value)
                    : null,

                Customer = MakeCustomerMini(resolvedAddressNoEx),
                Team = teamMini,

                ProfessionalIds = baseProfessionalIds,
                ProfessionalId = baseProfessionalIds.Any() ? baseProfessionalIds.First() : null
            });
        }
    }

    return Ok(outList.OrderBy(x => x.Start).ToList());
}

/// <summary>
/// Atualiza uma ocorrência recorrente por InstanceId (sem o front precisar calcular OccurrenceStart).
/// scope: This / ThisAndFollowing / All
/// </summary>
[HttpPut("instance/{instanceId}")]
public async Task<IActionResult> UpdateInstance(string instanceId, [FromBody] UpdateAppointmentDTO dto)
{
    if (!TryDecodeInstanceId(instanceId, out var seriesId, out var occStart))
        return BadRequest("InstanceId inválido.");

    var anchor = await _db.Set<Appointment>()
        .FirstOrDefaultAsync(a => a.IsRecurring && a.SeriesId == seriesId);

    if (anchor == null) return NotFound();

    // Força a identificação da ocorrência clicada
    dto.OccurrenceStart = occStart;

    // Reaproveita a lógica de Update por ID
    return await Update(anchor.Id, dto);
}



/// <summary>
/// Envia SMS "On my way" para uma ocorrência recorrente (InstanceId) via Twilio.
/// Aplica overrides (endereço / profissionais) e respeita escopo (admin/company/professional).
/// </summary>
[HttpPost("instance/{instanceId}/on-my-way-sms")]
public async Task<IActionResult> SendOnMyWaySmsInstance(
    string instanceId,
    [FromQuery] int? etaMinutes,
    [FromBody] OnMyWaySmsRequestDTO? request,
    CancellationToken ct)
{
    if (!TryDecodeInstanceId(instanceId, out var seriesId, out var occStart))
        return BadRequest("InstanceId inválido.");

    var anchor = await _db.Set<Appointment>()
        .Include(a => a.Company)
        .Include(a => a.Customer)
        .Include(a => a.CustomerAddress)
        .FirstOrDefaultAsync(a => a.IsRecurring && a.SeriesId == seriesId, ct);

    if (anchor == null) return NotFound("Série recorrente não encontrada.");

    var ex = await _db.Set<AppointmentRecurrenceException>().AsNoTracking()
        .Where(e => e.SeriesId == seriesId && e.OccurrenceStart == occStart)
        .OrderByDescending(e => e.UpdatedDate)
        .FirstOrDefaultAsync(ct);

    if (ex != null && ex.IsCancelled)
        return BadRequest("Ocorrência cancelada.");

    // ProfessionalIds finais (override tem prioridade se vier preenchido)
    var finalProfessionalIds = (ex?.OverrideProfessionalIds != null && ex.OverrideProfessionalIds.Any())
        ? ex.OverrideProfessionalIds.Distinct().ToList()
        : (anchor.ProfessionalIds?.Distinct().ToList() ?? new List<int>());

    // Checagem de permissão para esta instância
    if (!_currentUser.IsAdmin)
    {
        if (_currentUser.IsCompany)
        {
            if (!_currentUser.CompanyId.HasValue || anchor.CompanyId != _currentUser.CompanyId.Value)
                return Forbid();
        }
        else if (_currentUser.IsProfessional)
        {
            if (!_currentUser.ProfessionalId.HasValue || !finalProfessionalIds.Contains(_currentUser.ProfessionalId.Value))
                return Forbid();
        }
        else
        {
            return Forbid();
        }
    }

    var companyName = anchor.Company?.Name ?? "Our Team";
    var companyPhone = anchor.Company?.Phone ?? "";
    var companyEmail = anchor.Company?.Email ?? "";
    var customerName = anchor.Customer?.Name ?? "there";
    var to = anchor.Customer?.Phone ?? string.Empty;

    if (string.IsNullOrWhiteSpace(to))
        return BadRequest("Customer não possui telefone para envio de SMS.");

    // Address resolution order:
    // 1) Exception OverrideAddress
    // 2) Exception OverrideCustomerAddressId
    // 3) Anchor.Address (snapshot/string)
    // 4) Anchor.CustomerAddress (FK)
    // 5) Customer.Address (legacy)
    string address = string.Empty;
    if (!string.IsNullOrWhiteSpace(ex?.OverrideAddress))
    {
        address = ex!.OverrideAddress!;
    }
    else if (ex?.OverrideCustomerAddressId != null)
    {
        var overrideAddr = await _db.Set<CustomerAddress>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ca => ca.Id == ex.OverrideCustomerAddressId.Value && ca.CustomerId == anchor.CustomerId, ct);

        if (overrideAddr != null)
            address = BuildCustomerAddressLine(overrideAddr);
    }

    if (string.IsNullOrWhiteSpace(address))
        address = !string.IsNullOrWhiteSpace(anchor.Address)
            ? anchor.Address
            : (!string.IsNullOrWhiteSpace(anchor.CustomerAddress?.AddressLine1)
                ? BuildCustomerAddressLine(anchor.CustomerAddress)
                : (anchor.Customer?.Address ?? string.Empty));

    var eta = request?.EtaMinutes ?? etaMinutes ?? 15;

    if (eta < 1 || eta > 240)
        return BadRequest("etaMinutes deve estar entre 1 e 240 minutos.");

    if (string.IsNullOrWhiteSpace(address))
        return BadRequest("Não foi possível determinar o endereço da ocorrência.");

    // Resolve recipient language (Customer.Language → Company.Language → "en")
    var customerIdFromAnchor = anchor.CustomerId ?? 0;
    var language = customerIdFromAnchor > 0
        ? await _langResolver.ForCustomerAsync(customerIdFromAnchor, ct)
        : await _langResolver.ForCompanyAsync(anchor.CompanyId, ct);

    var body = _loc.Get("sms.onMyWay.body", language, new
    {
        customer = customerName,
        minutes = eta,
        company = companyName,
        address = address
    });
    body = "DON'T REPLY. " + body + " Reply STOP to unsubscribe.";

try
    {
        var (sid, _) = await _sms.SendSmsAsync(to, body, ct);

        return Ok(new
        {
            instanceId,
            appointmentId = anchor.Id,
            to,
            messageSid = sid,
            body
        });
    }
    catch (TwilioValidationException twEx)
    {
        return BadRequest(twEx.Message);
    }
    catch (TwilioConfigurationException cfgEx)
    {
        return StatusCode(500, cfgEx.Message);
    }
    catch (TwilioRequestException)
    {
        return StatusCode(502, "Falha ao enviar SMS via Twilio. Verifique a configuração e o número do destinatário.");
    }
}
/// <summary>
/// Deleta uma ocorrência recorrente por InstanceId (sem o front precisar calcular OccurrenceStart).
/// </summary>
[HttpDelete("instance/{instanceId}")]
public async Task<IActionResult> DeleteInstance(
    string instanceId,
    [FromQuery] RecurrenceScope scope = RecurrenceScope.This)
{
    if (!TryDecodeInstanceId(instanceId, out var seriesId, out var occStart))
        return BadRequest("InstanceId inválido.");

    var anchor = await _db.Set<Appointment>()
        .FirstOrDefaultAsync(a => a.IsRecurring && a.SeriesId == seriesId);

    if (anchor == null) return NotFound();

    return await Delete(anchor.Id, scope, occStart, null);
}
// ---------------- helpers ----------------

        private static TimeZoneInfo ResolveTimeZone(string? tz)
        {
            if (string.IsNullOrWhiteSpace(tz)) return TimeZoneInfo.Utc;
            try { return TimeZoneInfo.FindSystemTimeZoneById(tz); }
            catch { return TimeZoneInfo.Utc; }
        }

        private static DateTime ToUtc(DateTime local, TimeZoneInfo tz)
            => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

        private static DateTime FromUtc(DateTime utc, TimeZoneInfo tz)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);



        private (DateTime occStart, DateTime occEnd) ResolveOccurrenceWindow(
            Appointment anchor, DateTime occurrenceStart, DateTime? occurrenceEndOverride)
        {
            var duration = anchor.End - anchor.Start;
            var occEnd = occurrenceEndOverride ?? (occurrenceStart + duration);
            return (occurrenceStart, occEnd);
        }

        private async Task UpsertExceptionCancellationAsync(
            Appointment anchor, DateTime occurrenceStart, DateTime? occurrenceEndOverride)
        {
            var seriesId = anchor.SeriesId!.Value;
            var (occStart, occEnd) = ResolveOccurrenceWindow(anchor, occurrenceStart, occurrenceEndOverride);

            var ex = await _db.Set<AppointmentRecurrenceException>()
                .FirstOrDefaultAsync(e => e.SeriesId == seriesId && e.OccurrenceStart == occStart);

            if (ex == null)
            {
                ex = new AppointmentRecurrenceException
                {
                    SeriesId = seriesId,
                    OccurrenceStart = occStart,
                    OccurrenceEnd = occEnd,
                    IsCancelled = true
                };
                await _db.Set<AppointmentRecurrenceException>().AddAsync(ex);
            }
            else
            {
                ex.OccurrenceEnd = occEnd;
                ex.IsCancelled = true;
            }
        }

        private async Task UpsertExceptionOverrideAsync(
            Appointment anchor, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            var seriesId = anchor.SeriesId!.Value;
            var occStart = dto.OccurrenceStart!.Value;
            var (windowStart, windowEnd) = ResolveOccurrenceWindow(anchor, occStart, dto.OccurrenceEnd);

            var ex = await _db.Set<AppointmentRecurrenceException>()
                .FirstOrDefaultAsync(e => e.SeriesId == seriesId && e.OccurrenceStart == windowStart);

            if (ex == null)
            {
                ex = new AppointmentRecurrenceException
                {
                    SeriesId = seriesId,
                    OccurrenceStart = windowStart,
                    OccurrenceEnd = windowEnd,
                    IsCancelled = false
                };
                await _db.Set<AppointmentRecurrenceException>().AddAsync(ex);
            }
            else
            {
                ex.OccurrenceEnd = windowEnd;
                ex.IsCancelled = false;
            }

            if (dto.Title != null) ex.OverrideTitle = dto.Title;
            if (dto.Address != null) ex.OverrideAddress = dto.Address;
            if (dto.CustomerAddressId.HasValue)
                ex.OverrideCustomerAddressId = dto.CustomerAddressId.Value <= 0 ? null : dto.CustomerAddressId.Value;
            if (dto.Notes != null) ex.OverrideNotes = dto.Notes;

            if (dto.Start.HasValue && dto.End.HasValue)
            {
                ex.OverrideStart = dto.Start.Value;
                ex.OverrideEnd = dto.End.Value;
            }

            if (dto.Status.HasValue) ex.OverrideStatus = dto.Status.Value;
            if (dto.Type.HasValue) ex.OverrideType = dto.Type.Value;

            if (dto.ServiceTypeId.HasValue)
            {
                var stId = dto.ServiceTypeId.Value;
                ex.OverrideServiceTypeId = stId <= 0 ? null : stId;
                await ValidateServiceTypeForCompanyAsync(anchor.CompanyId, ex.OverrideServiceTypeId);
            }

            if (dto.ProfessionalIds != null)
                ex.OverrideProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
        }

        private async Task CutSeriesAsync(Appointment anchor, DateTime occurrenceStart)
        {
            var seriesId = anchor.SeriesId!.Value;

            // If the cut is at/before the first occurrence, delete the whole series
            if (occurrenceStart <= anchor.Start)
            {
                var exAll = await _db.Set<AppointmentRecurrenceException>()
                    .Where(e => e.SeriesId == seriesId)
                    .ToListAsync();

                _db.Set<AppointmentRecurrenceException>().RemoveRange(exAll);
                _db.Set<Appointment>().Remove(anchor);
                return;
            }

            var cutEnd = occurrenceStart.AddTicks(-1);

            if (!anchor.RecurrenceEnd.HasValue || anchor.RecurrenceEnd.Value > cutEnd)
                anchor.RecurrenceEnd = cutEnd;

            // Prefer end-date bounded series after a cut
            anchor.OccurrenceCount = null;

            // Remove exceptions that are now beyond the new end
            var future = await _db.Set<AppointmentRecurrenceException>()
                .Where(e => e.SeriesId == seriesId && e.OccurrenceStart >= occurrenceStart)
                .ToListAsync();

            _db.Set<AppointmentRecurrenceException>().RemoveRange(future);
        }
        private Appointment MapAppointment(
            CreateAppointmentDTO dto, DateTime start, DateTime end, TimeZoneInfo tz, bool isRecurring, Guid? seriesId)
        {
            // Mantém compatibilidade: Category é o campo novo no front; se não vier, usa o Type legado.
            var category = dto.Category;
            if (string.IsNullOrWhiteSpace(category) && dto.Type.HasValue)
                category = dto.Type.Value.ToString();

            var appointment = new Appointment
            {
                Title = dto.Title,
                Address = dto.Address,
                Notes = dto.Notes,
                Start = start,
                End = end,
                TimeZoneId = tz.Id,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                CustomerAddressId = dto.CustomerAddressId,
                TeamId = dto.TeamId,
                Status = dto.Status ?? Core.Enums.Appointment.AppointmentStatus.Scheduled,
                Type   = dto.Type   ?? Core.Enums.Appointment.AppointmentType.Regular,
                Category = category,
                ServiceTypeId = dto.ServiceTypeId,
                IsRecurring = isRecurring,
                RecurrenceRule = dto.RecurrenceRule,
                SeriesId = seriesId,
                RecurrenceEnd = dto.RecurrenceEnd,
                OccurrenceCount = dto.OccurrenceCount,
                IsException = false
            };

            // Atribui lista de profissionais, se enviada
            if (dto.ProfessionalIds != null)
            {
                appointment.ProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
            }

            return appointment;
        }

        /// <summary>
        /// Limpa registros dependentes de Appointment quando o relacionamento está configurado com DeleteBehavior.Restrict
        /// (ex.: PayrollItems e AppointmentCompletions).
        ///
        /// Isso evita o erro:
        /// 23503: update or delete on table "Appointments" violates foreign key constraint ...
        /// </summary>
        private async Task CleanupAppointmentReferencesAsync(int appointmentId)
        {
            // IMPORTANT:
            // Não materialize PayrollItem aqui.
            // Em alguns bancos antigos ainda não existe a coluna CustomerAddressId em PayrollItems,
            // e o EF tenta selecioná-la ao fazer ToListAsync(), causando:
            // 42703: column p.CustomerAddressId does not exist

            // 1) Remove PayrollItems do appointment (DELETE direto, sem SELECT)
            await _db.Set<PayrollItem>()
                .Where(i => i.AppointmentId == appointmentId)
                .ExecuteDeleteAsync();

            // 2) Remove snapshots de completion do appointment (DELETE direto)
            await _db.Set<AppointmentCompletion>()
                .Where(c => c.AppointmentId == appointmentId)
                .ExecuteDeleteAsync();
        }

        /// <summary>
        /// Remove itens/snapshots referentes a UMA ocorrência específica (AppointmentId + OccurrenceStart).
        /// Útil quando o usuário exclui apenas uma instância da série (scope=This).
        /// </summary>
        private async Task CleanupAppointmentOccurrenceReferencesAsync(int appointmentId, DateTime occurrenceStart)
        {
            // PayrollItems são únicos por (PayrollRunId, ProfessionalId, AppointmentId, OccurrenceStart)
            await _db.Set<PayrollItem>()
                .Where(i => i.AppointmentId == appointmentId && i.OccurrenceStart == occurrenceStart)
                .ExecuteDeleteAsync();

            // Completion snapshot é único por (CompanyId, AppointmentId, OccurrenceStart)
            // Busca somente o Id (evita SELECT de colunas inexistentes por versões antigas)
            var completionId = await _db.Set<AppointmentCompletion>()
                .Where(c => c.AppointmentId == appointmentId && c.OccurrenceStart == occurrenceStart)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (completionId.HasValue)
            {
                // Se houver PayrollItems apontando por AppointmentCompletionId, remove antes.
                await _db.Set<PayrollItem>()
                    .Where(i => i.AppointmentCompletionId == completionId.Value)
                    .ExecuteDeleteAsync();

                await _db.Set<AppointmentCompletion>()
                    .Where(c => c.Id == completionId.Value)
                    .ExecuteDeleteAsync();
            }
        }

        /// <summary>
        /// Remove itens/snapshots a partir de uma ocorrência (>= occurrenceStart).
        /// Útil no delete scope=ThisAndFollowing.
        /// </summary>
        private async Task CleanupAppointmentOccurrenceReferencesFromAsync(int appointmentId, DateTime occurrenceStart)
        {
            await _db.Set<PayrollItem>()
                .Where(i => i.AppointmentId == appointmentId && i.OccurrenceStart >= occurrenceStart)
                .ExecuteDeleteAsync();

            // Pega somente os IDs das completions
            var completionIds = await _db.Set<AppointmentCompletion>()
                .Where(c => c.AppointmentId == appointmentId && c.OccurrenceStart >= occurrenceStart)
                .Select(c => c.Id)
                .ToListAsync();

            if (completionIds.Count > 0)
            {
                await _db.Set<PayrollItem>()
                    .Where(i => i.AppointmentCompletionId.HasValue && completionIds.Contains(i.AppointmentCompletionId.Value))
                    .ExecuteDeleteAsync();

                await _db.Set<AppointmentCompletion>()
                    .Where(c => c.AppointmentId == appointmentId && c.OccurrenceStart >= occurrenceStart)
                    .ExecuteDeleteAsync();
            }
        }

        private async Task ValidateServiceTypeForCompanyAsync(int companyId, int? serviceTypeId)
        {
            if (!serviceTypeId.HasValue) return;

            var st = await _db.Set<ServiceType>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serviceTypeId.Value);

            if (st == null)
                throw new BadRequestException("ServiceTypeId inválido.");

            if (st.CompanyId != companyId)
                throw new ForbiddenException("ServiceType não pertence a esta company.");
        }

        // Simple RRULE expansion supporting DAILY and WEEKLY with INTERVAL, BYDAY, COUNT, UNTIL
        private List<(DateTime start, DateTime end)> ExpandOccurrences(
            string rrule, DateTime startLocal, DateTime endLocal, DateTime? endLocalSeries, int? count, TimeZoneInfo tz)
        {
            var rule = ParseRRule(rrule);
            var list = new List<(DateTime, DateTime)>();

            var duration = endLocal - startLocal;
            var occurrences = 0;

            DateTime cursor = startLocal;
            var timeOfDay = startLocal.TimeOfDay;

            if (rule.Freq == "DAILY")
            {
                int interval = rule.Interval;
                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);
                while (cursor <= limit && (count == null || occurrences < count.Value))
                {
                    var start = cursor;
                    var end = cursor + duration;
                    list.Add((start, end));
                    occurrences += 1;
                    cursor = cursor.AddDays(interval);
                }
            }
            else if (rule.Freq == "WEEKLY")
            {
                int interval = rule.Interval;
                var days = rule.ByDay; // e.g., ["MO","WE","FR"]
                if (days.Count == 0) days = new List<string> { DayToByDay(cursor.DayOfWeek) };
                days = days
                    .Select(d => d.ToUpperInvariant())
                    .Distinct()
                    .OrderBy(DaySortKey)
                    .ToList();

                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);
                DateTime weekStart = cursor.Date;
                while (weekStart <= limit && (count == null || occurrences < count.Value))
                {
                    foreach (var d in days)
                    {
                        // Next occurrence for this BYDAY in the current week
                        DateTime dayDate = NextOnOrAfter(weekStart, d);
                        if (dayDate < startLocal.Date) continue;
                        if (dayDate > limit) break;

                        var startCandidate = dayDate.Date + timeOfDay;

                        if (startCandidate < startLocal) continue;
                        if (endLocalSeries.HasValue && startCandidate > endLocalSeries.Value) continue;
                        if (count != null && occurrences >= count.Value) break;

                        var start = startCandidate;
                        var end = startCandidate + duration;
                        list.Add((start, end));
                        occurrences++;
                    }

                    weekStart = weekStart.AddDays(7 * interval);
                }
            }
            else if (rule.Freq == "MONTHLY")
            {
                int interval = rule.Interval;
                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);

                // Por padrão, repete no mesmo dia do mês do START.
                // Se BYMONTHDAY vier, usa ele (suporta 1 valor).
                var monthDays = rule.ByMonthDay;
                int targetDay = monthDays.Count > 0 ? monthDays[0] : startLocal.Day;

                // Começa do mês do startLocal
                var monthCursor = new DateTime(startLocal.Year, startLocal.Month, 1, 0, 0, 0, startLocal.Kind);

                while (monthCursor <= limit && (count == null || occurrences < count.Value))
                {
                    var daysInMonth = DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
                    if (targetDay >= 1 && targetDay <= daysInMonth)
                    {
                        var dayDate = new DateTime(monthCursor.Year, monthCursor.Month, targetDay, 0, 0, 0, monthCursor.Kind);
                        var startCandidate = dayDate + timeOfDay;

                        if (startCandidate >= startLocal && startCandidate <= limit)
                        {
                            list.Add((startCandidate, startCandidate + duration));
                            occurrences++;
                        }
                    }

                    monthCursor = monthCursor.AddMonths(interval);
                }
            }
            else
            {
                // Fallback: single occurrence
                list.Add((startLocal, endLocal));
            }

            // UNTIL cap (local)
            if (endLocalSeries.HasValue)
            {
                list = list.Where(o => o.Item1 <= endLocalSeries.Value).ToList();
            }

            // Remove duplicatas e ordena (garante estabilidade no calendário)
            // NOTE: usamos Item1/Item2 porque a tupla pode não estar nomeada.
            // Se a lista for List<(DateTime start, DateTime end)> então x.start também funciona,
            // mas Item1 é compatível em ambos os casos.
            return list
                .GroupBy(x => x.Item1)
                .Select(g => g.First())
                .OrderBy(x => x.Item1)
                .ToList();
        }

        private static int DaySortKey(string byday)
        {
            return byday.ToUpperInvariant() switch
            {
                "MO" => 1,
                "TU" => 2,
                "WE" => 3,
                "TH" => 4,
                "FR" => 5,
                "SA" => 6,
                "SU" => 7,
                _ => 8
            };
        }


        private static string DayToByDay(DayOfWeek dow)
        {
            return dow switch
            {
                DayOfWeek.Monday => "MO",
                DayOfWeek.Tuesday => "TU",
                DayOfWeek.Wednesday => "WE",
                DayOfWeek.Thursday => "TH",
                DayOfWeek.Friday => "FR",
                DayOfWeek.Saturday => "SA",
                DayOfWeek.Sunday => "SU",
                _ => "MO"
            };
        }

        private static DateTime NextOnOrAfter(DateTime weekStart, string byday)
        {
            var target = byday.ToUpperInvariant();
            var map = new Dictionary<string, DayOfWeek> {
                ["MO"] = DayOfWeek.Monday,
                ["TU"] = DayOfWeek.Tuesday,
                ["WE"] = DayOfWeek.Wednesday,
                ["TH"] = DayOfWeek.Thursday,
                ["FR"] = DayOfWeek.Friday,
                ["SA"] = DayOfWeek.Saturday,
                ["SU"] = DayOfWeek.Sunday,
            };
            var targetDow = map.ContainsKey(target) ? map[target] : DayOfWeek.Monday;

            int diff = (int)targetDow - (int)weekStart.DayOfWeek;
            if (diff < 0) diff += 7;
            return weekStart.AddDays(diff);
        }

        private class RRule
        {
            public string Freq { get; set; } = "DAILY";
            public int Interval { get; set; } = 1;
            public List<string> ByDay { get; set; } = new List<string>();
            public List<int> ByMonthDay { get; set; } = new List<int>();
        }

        private static RRule ParseRRule(string rrule)
        {
            var r = new RRule();
            var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].ToUpperInvariant();
                var val = kv[1].ToUpperInvariant();

                if (key == "FREQ") r.Freq = val;
                else if (key == "INTERVAL" && int.TryParse(val, out var iv)) r.Interval = Math.Max(1, iv);
                else if (key == "BYDAY") r.ByDay = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                else if (key == "BYMONTHDAY")
                {
                    r.ByMonthDay = val
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => int.TryParse(x, out var md) ? md : 0)
                        .Where(x => x != 0)
                        .ToList();
                }
                // COUNT and UNTIL handled via parameters (dto.OccurrenceCount / dto.RecurrenceEnd)
            }
            return r;
        }

        private async Task UpdateThisAsync(Appointment current, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            if (!current.IsException)
            {
                current.IsException = true;
                current.OriginalStart = current.Start;
                current.OriginalEnd   = current.End;
            }

            if (dto.Title != null) current.Title = dto.Title;
            if (dto.Address != null) current.Address = dto.Address;
            if (dto.Notes != null) current.Notes = dto.Notes;
            if (dto.TimeZoneId != null) current.TimeZoneId = tz.Id;

            // Status / Type / Category / ServiceType
            if (dto.Status.HasValue) current.Status = dto.Status.Value;

            if (dto.Type.HasValue) current.Type = dto.Type.Value;
            if (dto.Category != null) current.Category = dto.Category;
            else if (dto.Type.HasValue) current.Category = dto.Type.Value.ToString();

            if (dto.ServiceTypeId.HasValue)
            {
                var normalizedServiceTypeId = dto.ServiceTypeId.Value <= 0 ? (int?)null : dto.ServiceTypeId.Value;
                await ValidateServiceTypeForCompanyAsync(current.CompanyId, normalizedServiceTypeId);
                current.ServiceTypeId = normalizedServiceTypeId;
            }

            // Relationships / Professionals
            if (dto.CustomerId.HasValue) current.CustomerId = dto.CustomerId.Value;
            if (dto.CustomerAddressId.HasValue) current.CustomerAddressId = dto.CustomerAddressId.Value <= 0 ? null : dto.CustomerAddressId.Value;
            if (dto.TeamId.HasValue) current.TeamId = dto.TeamId.Value;
            if (dto.ProfessionalIds != null) current.ProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
            if (dto.Start.HasValue && dto.End.HasValue)
            {
                current.Start = dto.Start.Value;
                current.End   = dto.End.Value;
            }
        }


                private async Task UpdateThisAndFollowingAsync(Appointment anchor, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            // "ThisAndFollowing" in a single-row series model:
            // We SPLIT the series into two series:
            // - a new "previous" anchor keeps the OLD SeriesId and is cut to end BEFORE occurrenceStart
            // - the current anchor keeps the SAME database Id, but receives a NEW SeriesId and starts at occurrenceStart
            // This keeps "1 row per series" while preserving the past.

            if (anchor.SeriesId == null)
            {
                await UpdateThisAsync(anchor, dto, tz);
                return;
            }

            if (!dto.OccurrenceStart.HasValue)
                throw new InvalidOperationException("OccurrenceStart é obrigatório para Scope=ThisAndFollowing.");

            var occStart = dto.OccurrenceStart.Value;

            // If the split point is at or before the first occurrence, treat as "All"
            if (occStart <= anchor.Start)
            {
                await UpdateAllAsync(anchor, dto, tz);
                return;
            }

            var oldSeriesId = anchor.SeriesId.Value;
            var newSeriesId = Guid.NewGuid();

            // Clone current anchor as the "previous" series (past)
            var previous = new Appointment
            {
                Title = anchor.Title,
                Address = anchor.Address,
                Notes = anchor.Notes,
                Start = anchor.Start,
                End = anchor.End,
                TimeZoneId = anchor.TimeZoneId,
                CompanyId = anchor.CompanyId,
                CustomerId = anchor.CustomerId,
                TeamId = anchor.TeamId,
                Status = anchor.Status,
                Type = anchor.Type,
                Category = anchor.Category ?? anchor.Type.ToString(),
                ServiceTypeId = anchor.ServiceTypeId,
                ProfessionalIdsData = anchor.ProfessionalIdsData,

                IsRecurring = true,
                RecurrenceRule = anchor.RecurrenceRule,
                SeriesId = oldSeriesId,
                RecurrenceEnd = occStart.AddTicks(-1),
                OccurrenceCount = null,
                IsException = false
            };

            await _db.Set<Appointment>().AddAsync(previous);

            // Move future exceptions to the new series id
            var futureExceptions = await _db.Set<AppointmentRecurrenceException>()
                .Where(e => e.SeriesId == oldSeriesId && e.OccurrenceStart >= occStart)
                .ToListAsync();

            foreach (var ex in futureExceptions)
                ex.SeriesId = newSeriesId;

            // Update current anchor to become the "future" series
            var duration = anchor.End - anchor.Start;

            anchor.SeriesId = newSeriesId;
            anchor.Start = dto.Start ?? occStart;
            anchor.End = dto.End ?? (anchor.Start + duration);

            // Apply remaining updates to the (new) series anchor
            await UpdateAllAsync(anchor, dto, tz);
        }
private async Task UpdateAllAsync(Appointment anchor, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            if (anchor.SeriesId == null)
            {
                await UpdateThisAsync(anchor, dto, tz);
                return;
            }

            var normalizedServiceTypeId = dto.ServiceTypeId.HasValue
                ? (dto.ServiceTypeId.Value <= 0 ? (int?)null : dto.ServiceTypeId.Value)
                : null;

            if (dto.ServiceTypeId.HasValue)
                await ValidateServiceTypeForCompanyAsync(anchor.CompanyId, normalizedServiceTypeId);

            var all = await _db.Set<Appointment>().Where(a => a.SeriesId == anchor.SeriesId).ToListAsync();
            foreach (var a in all)
            {
                if (a.IsException) continue;
                if (dto.Title != null) a.Title = dto.Title;
                if (dto.Address != null) a.Address = dto.Address;
                if (dto.Notes != null) a.Notes = dto.Notes;
                if (dto.TimeZoneId != null) a.TimeZoneId = tz.Id;

                // Status
                if (dto.Status.HasValue) a.Status = dto.Status.Value;

                // Category / Type / ServiceType (Payroll)
                if (dto.Type.HasValue) a.Type = dto.Type.Value;
                if (dto.Category != null) a.Category = dto.Category;
                else if (dto.Type.HasValue) a.Category = dto.Type.Value.ToString();
                if (dto.ServiceTypeId.HasValue) a.ServiceTypeId = normalizedServiceTypeId;

                if (dto.ProfessionalIds != null)
                    a.ProfessionalIds = dto.ProfessionalIds.Distinct().ToList();

                if (dto.Start.HasValue && dto.End.HasValue)
                {
                    a.Start = dto.Start.Value;
                    a.End   = dto.End.Value;
                }
                if (dto.IsRecurring.HasValue) a.IsRecurring = dto.IsRecurring.Value;
                if (dto.RecurrenceRule != null) a.RecurrenceRule = dto.RecurrenceRule;
                if (dto.RecurrenceEnd.HasValue) a.RecurrenceEnd = dto.RecurrenceEnd.Value;
                if (dto.OccurrenceCount.HasValue) a.OccurrenceCount = dto.OccurrenceCount.Value;
            }
        }

private static string EncodeInstanceId(Guid seriesId, DateTime occurrenceStart)
{
    // occurrenceStart é armazenado como horário local/unspecified no padrão do projeto
    return $"rec_{seriesId:N}_{occurrenceStart.Ticks}";
}

private static bool TryDecodeInstanceId(string instanceId, out Guid seriesId, out DateTime occurrenceStart)
{
    seriesId = default;
    occurrenceStart = default;

    if (string.IsNullOrWhiteSpace(instanceId)) return false;
    var parts = instanceId.Split('_', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 3) return false;
    if (!parts[0].Equals("rec", StringComparison.OrdinalIgnoreCase)) return false;

    if (!Guid.TryParseExact(parts[1], "N", out seriesId)) return false;
    if (!long.TryParse(parts[2], out var ticks)) return false;

    try
    {
        occurrenceStart = new DateTime(ticks, DateTimeKind.Unspecified);
        return true;
    }
    catch
    {
        return false;
    }
}



private async Task RecordCompletionSnapshotIfNeededAsync(Appointment anchor, DateTime occurrenceStart, DateTime occurrenceEnd, List<int>? professionalIdsOverride)
{
    var effectiveProfessionalIds = (professionalIdsOverride ?? new List<int>())
        .Where(id => id > 0)
        .Distinct()
        .ToList();

    if (effectiveProfessionalIds.Count == 0 && anchor.ProfessionalIds != null && anchor.ProfessionalIds.Count > 0)
        effectiveProfessionalIds = anchor.ProfessionalIds.Distinct().ToList();

    if (effectiveProfessionalIds.Count == 0 && anchor.TeamId.HasValue)
    {
        var teamMembers = await _db.Set<TeamMember>()
            .AsNoTracking()
            .Where(m => m.TeamId == anchor.TeamId.Value)
            .Select(m => m.ProfessionalId)
            .ToListAsync();

        effectiveProfessionalIds = teamMembers.Distinct().ToList();
    }

    // Basic scope check (defensive)
    if (!_currentUser.IsAdmin)
    {
        if (_currentUser.IsCompany && _currentUser.CompanyId != anchor.CompanyId)
            throw new ForbiddenException("Você não tem acesso a esta empresa.");

        if (_currentUser.IsProfessional && (_currentUser.ProfessionalId == null || !effectiveProfessionalIds.Contains(_currentUser.ProfessionalId.Value)))
            throw new ForbiddenException("Você não tem acesso a este agendamento.");
    }

    var exists = await _db.AppointmentCompletions
        .AsNoTracking()
        .AnyAsync(x => x.AppointmentId == anchor.Id && x.OccurrenceStart == occurrenceStart);

    if (exists) return;

    decimal sourceAmount = 0m;
    Customer? customer = null;
    if (anchor.CustomerId.HasValue)
        customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == anchor.CustomerId.Value);

    CustomerAddress? addr = null;
    if (anchor.CustomerAddressId.HasValue)
        addr = await _db.CustomerAddresses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == anchor.CustomerAddressId.Value);
    if (addr == null && anchor.CustomerId.HasValue)
        addr = await _db.CustomerAddresses.AsNoTracking().FirstOrDefaultAsync(a => a.CustomerId == anchor.CustomerId.Value && a.IsPrimary);

    if (addr != null) sourceAmount = addr.Ticket ?? 0m;
    else if (customer != null) sourceAmount = customer.Ticket ?? 0m;

    var completion = new AppointmentCompletion
    {
        CompanyId = anchor.CompanyId,
        AppointmentId = anchor.Id,
        SeriesId = anchor.SeriesId,
        OccurrenceStart = occurrenceStart,
        OccurrenceEnd = occurrenceEnd,
        CompletedAt = DateTime.UtcNow,
        CustomerIdSnapshot = anchor.CustomerId,
        CustomerAddressIdSnapshot = addr?.Id,
        TeamIdSnapshot = anchor.TeamId,
        CategorySnapshot = anchor.Category ?? anchor.Type.ToString(),
        ServiceTypeIdSnapshot = anchor.ServiceTypeId,
        SourceAmountSnapshot = sourceAmount,
        CustomerAddressSnapshot = addr != null ? $"{addr.AddressLine1} - {addr.City}/{addr.State}" : null,
        PaymentMethodSnapshot = addr?.PaymentMethod,
        FrequencySnapshot = addr?.Frequency,
        ProfessionalIdsSnapshot = effectiveProfessionalIds
    };

    _db.AppointmentCompletions.Add(completion);
}
    }
}