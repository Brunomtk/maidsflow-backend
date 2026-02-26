using System.Threading;
using System.Threading.Tasks;

namespace Services.Email;

public interface IWelcomeEmailService
{
    Task SendWelcomeAsync(string toEmail, string? toName, string? loginUrl, CancellationToken ct = default);
}
