using System.Threading;
using System.Threading.Tasks;

namespace Services.Email;

public interface IPlanPaymentEmailService
{
    Task SendPlanPaymentSuccessAsync(
        int companyId,
        decimal amountPaid,
        string currency,
        string? invoiceNumber,
        string? hostedInvoiceUrl,
        string? invoicePdfUrl,
        long? periodStartUnix,
        long? periodEndUnix,
        long? paidAtUnix,
        CancellationToken ct = default);

    Task SendPlanPaymentFailedAsync(
        int companyId,
        decimal amountDue,
        string currency,
        string? invoiceNumber,
        string? hostedInvoiceUrl,
        string? invoicePdfUrl,
        long? periodStartUnix,
        long? periodEndUnix,
        long? failedAtUnix,
        long? nextPaymentAttemptUnix,
        CancellationToken ct = default);
}
