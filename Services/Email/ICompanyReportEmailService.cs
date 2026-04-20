using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Reports;

namespace Services.Email;

public interface ICompanyReportEmailService
{
    Task<SendCompanyReportEmailResultDto> SendAsync(int companyId, SendCompanyReportEmailRequestDto request, string triggeredBy, CancellationToken ct = default);
}
