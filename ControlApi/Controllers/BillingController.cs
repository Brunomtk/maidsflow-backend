using System.IO;
using System.Threading.Tasks;
using Core.DTO.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Integrations.Stripe;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillingController : ControllerBase
    {
        private readonly IStripeBillingService _stripe;

        public BillingController(IStripeBillingService stripe)
        {
            _stripe = stripe;
        }

        /// <summary>
        /// Lista preços recorrentes ativos do Stripe (para seleção de planos na UI).
        /// </summary>
        [Authorize]
        [HttpGet("stripe-prices")]
        public async Task<IActionResult> ListStripePrices()
        {
            var prices = await _stripe.ListActiveRecurringPricesAsync();
            return Ok(prices);
        }

        /// <summary>
        /// Cria uma Checkout Session (Stripe) para assinatura de um plano.
        /// Retorna a URL do Stripe Checkout para redirecionamento.
        /// </summary>
        [Authorize]
        [HttpPost("checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateStripeCheckoutSessionRequest request)
        {
            var result = await _stripe.CreateCheckoutSessionAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Confirma uma Checkout Session (client-to-server) e garante o registro local em PlanSubscriptions
        /// com StripeSubscriptionId preenchido. Útil para o fluxo onde a UI volta do Stripe com ?session_id=...
        /// </summary>
        [Authorize]
        [HttpPost("confirm-checkout")]
        public async Task<IActionResult> ConfirmCheckout([FromBody] ConfirmStripeCheckoutSessionRequest request)
        {
            await _stripe.ConfirmCheckoutSessionAsync(request);
            return Ok(new { ok = true });
        }

        /// <summary>
        /// Histórico de cobranças (Stripe Invoices) da empresa.
        /// Company: usa a company do token.
        /// Admin: pode informar companyId.
        /// </summary>
        [Authorize]
        [HttpGet("billing-history")]
        public async Task<IActionResult> GetBillingHistory([FromQuery] int? companyId = null, [FromQuery] int limit = 20)
        {
            var items = await _stripe.GetBillingHistoryAsync(companyId, limit);
            return Ok(items);
        }

        /// <summary>
        /// Resumo do Stripe: últimas assinaturas do customer, saldo (wallet/account_balance) e próxima cobrança (upcoming invoice).
        /// Company: usa a company do token.
        /// Admin: pode informar companyId.
        /// </summary>
        [Authorize]
        [HttpGet("stripe-summary")]
        public async Task<IActionResult> GetStripeSummary([FromQuery] int? companyId = null, [FromQuery] int subscriptionsLimit = 10)
        {
            var summary = await _stripe.GetStripeBillingSummaryAsync(companyId, subscriptionsLimit);
            return Ok(summary);
        }

        /// <summary>
        /// Webhook do Stripe (server-to-server). Não requer autenticação.
        /// Configure o endpoint no Stripe e o segredo em Stripe:WebhookSecret.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("stripe-webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var sigHeader = Request.Headers["Stripe-Signature"].ToString();

            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            await _stripe.HandleWebhookAsync(json, sigHeader);
            return Ok();
        }
    }
}
