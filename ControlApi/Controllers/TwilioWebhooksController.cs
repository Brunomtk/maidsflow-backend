using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Integrations.Twilio;

namespace ControlApi.Controllers
{
    /// <summary>
    /// Public endpoints called by Twilio when SMS messages arrive at our numbers
    /// (inbound webhooks). Handles compliance keywords automatically:
    ///
    ///   STOP / STOPALL / UNSUBSCRIBE / CANCEL / END / QUIT
    ///       → Twilio also stops at the carrier; we mirror by setting Customer.ReceiveSms = false.
    ///
    ///   START / UNSTOP / YES
    ///       → Twilio re-enables; we flip Customer.ReceiveSms = true again.
    ///
    ///   HELP / INFO
    ///       → handled by Twilio itself, we just acknowledge.
    ///
    /// Endpoint URL to configure on the Twilio number:
    ///   https://api.maidsflow.com/api/Webhooks/Twilio/InboundSms
    ///
    /// Twilio will POST application/x-www-form-urlencoded with at least:
    ///   From=+15551234567&To=+18443146425&Body=START&MessageSid=SMxxxxx&AccountSid=ACxxxxx
    ///
    /// We respond with empty TwiML so Twilio doesn't auto-reply on top of its own STOP/START response.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/Webhooks/Twilio")]
    public class TwilioWebhooksController : ControllerBase
    {
        private readonly DbContextClass _db;
        private readonly ILogger<TwilioWebhooksController> _logger;

        public TwilioWebhooksController(DbContextClass db, ILogger<TwilioWebhooksController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Twilio always POSTs form-encoded.
        [HttpPost("InboundSms")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> InboundSms([FromForm] InboundSmsForm form, CancellationToken ct)
        {
            var from = form.From?.Trim();
            var body = (form.Body ?? string.Empty).Trim();
            _logger.LogInformation("Twilio inbound SMS from {From} to {To}: {Body}", from, form.To, body);

            if (string.IsNullOrWhiteSpace(from))
                return EmptyTwiml();

            // Normalize sender to E.164
            string fromE164;
            try
            {
                fromE164 = PhoneNumberUtils.NormalizeToE164OrThrow(from, nameof(form.From));
            }
            catch
            {
                _logger.LogWarning("Inbound SMS with un-normalizable From: {From}", from);
                return EmptyTwiml();
            }

            // Classify keyword (case-insensitive, first word only — same set Twilio recognizes)
            var firstWord = body.Split(new[] { ' ', '\t', '\r', '\n' }, 2,
                StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "";

            bool isStop = firstWord is "STOP" or "STOPALL" or "UNSUBSCRIBE" or "CANCEL" or "END" or "QUIT";
            bool isStart = firstWord is "START" or "UNSTOP" or "YES";

            if (!isStop && !isStart)
            {
                // Not a compliance keyword — nothing to do on our side. (HELP is handled by Twilio.)
                return EmptyTwiml();
            }

            // Find any customer with this phone (digits-only match, defends against formatting variants)
            var digits = new string(fromE164.Where(char.IsDigit).ToArray());
            var matches = await _db.Customers
                .Where(c => c.Phone != null)
                .ToListAsync(ct); // small enough; we'll filter in memory by digits

            var affected = matches.Where(c =>
                {
                    var cd = new string((c.Phone ?? "").Where(char.IsDigit).ToArray());
                    if (cd.Length == 0) return false;
                    // Compare by last 10 digits to be lenient about country code
                    var cdLast10 = cd.Length >= 10 ? cd[^10..] : cd;
                    var fLast10 = digits.Length >= 10 ? digits[^10..] : digits;
                    return cdLast10 == fLast10;
                }).ToList();

            if (affected.Count == 0)
            {
                _logger.LogInformation("Inbound {Keyword} from {From}: no matching customers.", firstWord, fromE164);
                return EmptyTwiml();
            }

            int changed = 0;
            foreach (var c in affected)
            {
                if (isStop && c.ReceiveSms)
                {
                    c.ReceiveSms = false;
                    changed++;
                }
                else if (isStart && !c.ReceiveSms)
                {
                    c.ReceiveSms = true;
                    changed++;
                }
            }

            if (changed > 0)
                await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Inbound {Keyword} from {From}: matched {Count} customers, updated {Changed}.",
                firstWord, fromE164, affected.Count, changed);

            return EmptyTwiml();
        }

        private ContentResult EmptyTwiml()
        {
            // Empty TwiML response (Twilio expects 200 OK with optional <Response/>)
            return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response/>", "application/xml");
        }

        public class InboundSmsForm
        {
            public string? From { get; set; }
            public string? To { get; set; }
            public string? Body { get; set; }
            public string? MessageSid { get; set; }
            public string? AccountSid { get; set; }
            public string? FromCountry { get; set; }
            public string? ToCountry { get; set; }
        }
    }
}
