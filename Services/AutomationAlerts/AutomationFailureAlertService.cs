using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.AutomationAlerts;
using Core.Exceptions;
using Core.Models;
using Core.Options;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;
using Services.Security;

namespace Services.AutomationAlerts
{
    public interface IAutomationFailureAlertService
    {
        Task<AutomationFailureLogDto> RecordAndNotifyAsync(CreateAutomationFailureAlertRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<AutomationFailureLogDto>> GetRecentAsync(int page, int pageSize, CancellationToken ct = default);
    }

    public class AutomationFailureAlertService : IAutomationFailureAlertService
    {
        private readonly DbContextClass _db;
        private readonly ICurrentUser _currentUser;
        private readonly IOptions<AutomationAlertsOptions> _options;
        private readonly ISendGridEmailSender _emailSender;

        public AutomationFailureAlertService(
            DbContextClass db,
            ICurrentUser currentUser,
            IOptions<AutomationAlertsOptions> options,
            ISendGridEmailSender emailSender)
        {
            _db = db;
            _currentUser = currentUser;
            _options = options;
            _emailSender = emailSender;
        }

        public async Task<AutomationFailureLogDto> RecordAndNotifyAsync(CreateAutomationFailureAlertRequest request, CancellationToken ct = default)
        {
            var opt = _options.Value;
            if (!opt.Enabled)
                throw new BadRequestException("Automation alerts are disabled.");

            var companyId = ResolveCompanyId(request.CompanyId);
            var recipientEmail = opt.DefaultRecipientEmail?.Trim();

            if (string.IsNullOrWhiteSpace(request.ErrorMessage))
                throw new BadRequestException("ErrorMessage is required.");

            var entity = new AutomationFailureLog
            {
                CompanyId = companyId,
                Source = string.IsNullOrWhiteSpace(request.Source) ? "n8n" : request.Source!.Trim(),
                WorkflowKey = string.IsNullOrWhiteSpace(request.WorkflowKey) ? Slugify(request.WorkflowName) : request.WorkflowKey!.Trim(),
                WorkflowName = string.IsNullOrWhiteSpace(request.WorkflowName) ? "Unnamed automation workflow" : request.WorkflowName!.Trim(),
                NodeName = request.NodeName?.Trim(),
                ErrorMessage = request.ErrorMessage!.Trim(),
                ErrorDetails = request.ErrorDetails?.Trim(),
                ExecutionId = request.ExecutionId?.Trim(),
                AppointmentId = request.AppointmentId,
                PayloadJson = NormalizePayloadJson(request.PayloadJson),
                AlertEmailTo = recipientEmail,
                OccurredAtUtc = request.OccurredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow,
                AlertEmailSent = false,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _db.AutomationFailureLogs.Add(entity);
            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                var subject = BuildSubject(opt, entity);
                var html = BuildHtml(entity);
                var text = BuildPlainText(entity);
                var send = await _emailSender.SendAsync(new SendGridEmailMessage(
                    ToEmail: recipientEmail,
                    Subject: subject,
                    PlainText: text,
                    Html: html,
                    ToName: opt.DefaultRecipientName
                ), ct);

                entity.AlertEmailSent = send.Ok;
                entity.AlertEmailSentAtUtc = send.Ok ? DateTime.UtcNow : null;
                if (!send.Ok && string.IsNullOrWhiteSpace(entity.ErrorDetails))
                {
                    entity.ErrorDetails = send.Error ?? send.ResponseBody ?? "Failed to send failure alert email.";
                }
                entity.UpdatedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return ToDto(entity);
        }

        public async Task<IReadOnlyList<AutomationFailureLogDto>> GetRecentAsync(int page, int pageSize, CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

            var query = _db.AutomationFailureLogs.AsNoTracking().AsQueryable();
            if (!_currentUser.IsAdmin && _currentUser.CompanyId.HasValue)
                query = query.Where(x => x.CompanyId == _currentUser.CompanyId.Value);

            return await query
                .OrderByDescending(x => x.OccurredAtUtc)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AutomationFailureLogDto
                {
                    Id = x.Id,
                    CompanyId = x.CompanyId,
                    Source = x.Source,
                    WorkflowKey = x.WorkflowKey,
                    WorkflowName = x.WorkflowName,
                    NodeName = x.NodeName,
                    ErrorMessage = x.ErrorMessage,
                    ErrorDetails = x.ErrorDetails,
                    ExecutionId = x.ExecutionId,
                    AppointmentId = x.AppointmentId,
                    AlertEmailTo = x.AlertEmailTo,
                    AlertEmailSent = x.AlertEmailSent,
                    OccurredAtUtc = x.OccurredAtUtc,
                    AlertEmailSentAtUtc = x.AlertEmailSentAtUtc,
                    CreatedDate = x.CreatedDate
                })
                .ToListAsync(ct);
        }

        private int? ResolveCompanyId(int? requestedCompanyId)
        {
            if (_currentUser.IsAdmin)
                return requestedCompanyId;

            if (_currentUser.CompanyId.HasValue)
                return _currentUser.CompanyId.Value;

            return requestedCompanyId;
        }

        private static string? NormalizePayloadJson(string? payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                return JsonSerializer.Serialize(doc.RootElement);
            }
            catch
            {
                return JsonSerializer.Serialize(payloadJson);
            }
        }

        private static string BuildSubject(AutomationAlertsOptions opt, AutomationFailureLog entity)
        {
            var parts = new List<string> { opt.SubjectPrefix.Trim(), entity.WorkflowName };
            return string.Join(" • ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static string BuildPlainText(AutomationFailureLog entity)
        {
            return $"Workflow failure detected.\n\nWorkflow: {entity.WorkflowName}\nWorkflowKey: {entity.WorkflowKey}\nSource: {entity.Source}\nNode: {entity.NodeName ?? "-"}\nAppointmentId: {(entity.AppointmentId?.ToString() ?? "-")}\nExecutionId: {entity.ExecutionId ?? "-"}\nOccurredAtUtc: {entity.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}Z\n\nError:\n{entity.ErrorMessage}\n\nDetails:\n{(string.IsNullOrWhiteSpace(entity.ErrorDetails) ? "-" : entity.ErrorDetails)}";
        }

        private static string BuildHtml(AutomationFailureLog entity)
        {
            string Enc(string? value) => HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(value) ? "-" : value);
            return $"""
<div style="font-family:Arial,sans-serif;font-size:14px;color:#16313d">
  <h2 style="margin:0 0 12px">Automation failure detected</h2>
  <p style="margin:0 0 16px">A workflow execution failed and triggered an alert.</p>
  <table cellpadding="6" cellspacing="0" style="border-collapse:collapse">
    <tr><td><strong>Workflow</strong></td><td>{Enc(entity.WorkflowName)}</td></tr>
    <tr><td><strong>Workflow Key</strong></td><td>{Enc(entity.WorkflowKey)}</td></tr>
    <tr><td><strong>Source</strong></td><td>{Enc(entity.Source)}</td></tr>
    <tr><td><strong>Node</strong></td><td>{Enc(entity.NodeName)}</td></tr>
    <tr><td><strong>Appointment Id</strong></td><td>{Enc(entity.AppointmentId?.ToString())}</td></tr>
    <tr><td><strong>Execution Id</strong></td><td>{Enc(entity.ExecutionId)}</td></tr>
    <tr><td><strong>Occurred At (UTC)</strong></td><td>{entity.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}Z</td></tr>
  </table>
  <div style="margin-top:16px;padding:12px;border-radius:8px;background:#f6f8fa;border:1px solid #d8dee4">
    <div style="font-weight:700;margin-bottom:8px">Error</div>
    <div>{Enc(entity.ErrorMessage)}</div>
  </div>
  <div style="margin-top:12px;padding:12px;border-radius:8px;background:#f6f8fa;border:1px solid #d8dee4">
    <div style="font-weight:700;margin-bottom:8px">Details</div>
    <div style="white-space:pre-wrap">{Enc(entity.ErrorDetails)}</div>
  </div>
</div>
""";
        }

        private static string Slugify(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "automation-workflow";
            var chars = value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            var slug = new string(chars);
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }

        private static AutomationFailureLogDto ToDto(AutomationFailureLog entity) => new AutomationFailureLogDto
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            Source = entity.Source,
            WorkflowKey = entity.WorkflowKey,
            WorkflowName = entity.WorkflowName,
            NodeName = entity.NodeName,
            ErrorMessage = entity.ErrorMessage,
            ErrorDetails = entity.ErrorDetails,
            ExecutionId = entity.ExecutionId,
            AppointmentId = entity.AppointmentId,
            AlertEmailTo = entity.AlertEmailTo,
            AlertEmailSent = entity.AlertEmailSent,
            OccurredAtUtc = entity.OccurredAtUtc,
            AlertEmailSentAtUtc = entity.AlertEmailSentAtUtc,
            CreatedDate = entity.CreatedDate
        };
    }
}
