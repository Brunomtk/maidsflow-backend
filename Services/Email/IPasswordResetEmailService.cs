using System.Threading;
using System.Threading.Tasks;

namespace Services.Email;

public interface IPasswordResetEmailService
{
    Task SendPasswordResetEmailAsync(int userId, string resetUrl, CancellationToken ct = default);
}
