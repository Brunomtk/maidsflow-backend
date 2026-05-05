using Core.DTO.Company;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Storage;
using System.Linq;
using System.Threading.Tasks;

namespace ControlApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CompaniesController : ControllerBase
    {
        private readonly ICompanyService _companyService;
        private readonly IS3StorageService _s3;

        public CompaniesController(ICompanyService companyService, IS3StorageService s3)
        {
            _companyService = companyService;
            _s3 = s3;
        }

        private CompanyDTO ToDto(Company c)
        {
            return new CompanyDTO
            {
                Id = c.Id,
                Name = c.Name,
                Cnpj = c.Cnpj,
                Responsible = c.Responsible,
                Email = c.Email,
                Phone = c.Phone,
                ReceiveSms = c.ReceiveSms,
                ReceiveEmail = c.ReceiveEmail,
                Language = c.Language,
                PlanId = c.PlanId,
                PlanName = c.Plan?.Name,
                AvatarKey = c.AvatarKey,
                AvatarUrl = string.IsNullOrWhiteSpace(c.AvatarKey) ? null : _s3.CreateDownloadUrl(c.AvatarKey),
                Status = c.Status,
                HasCompletedInitialSetup = c.HasCompletedInitialSetup,
                CreatedDate = c.CreatedDate,
                UpdatedDate = c.UpdatedDate
            };
        }

        // GET api/Companies
        [HttpGet]
        public async Task<IActionResult> GetAllCompanies()
        {
            var companies = await _companyService.GetAllCompanies();
            return Ok(companies.Select(ToDto));
        }

        // GET api/Companies/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCompanyById(int id)
        {
            var company = await _companyService.GetCompanyById(id);
            if (company == null) return NotFound();
            return Ok(ToDto(company));
        }

        // POST api/Companies
        [HttpPost]
        // Signup flow creates the Company before authentication, so this endpoint must be public.
        [AllowAnonymous]
        public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request)
        {
            // Signup endpoint is public. If a stale Authorization token is present in the browser,
            // the request becomes authenticated and may be blocked by role/scope guards.
            // Force an anonymous principal for this action to keep signup stable.
            HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

            var company = new Company
            {
                Name = request.Name,
                Cnpj = request.Cnpj,
                Responsible = request.Responsible,
                Email = request.Email,
                Phone = request.Phone,
                ReceiveSms = request.ReceiveSms,
                ReceiveEmail = request.ReceiveEmail,
                Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language,
                PlanId = request.PlanId,
                Status = request.Status,
                HasCompletedInitialSetup = request.HasCompletedInitialSetup ?? false
            };

            var ok = await _companyService.CreateCompany(company);
            if (!ok) return BadRequest("Unable to create company.");

            // IMPORTANT:
            // This endpoint is called anonymously during signup. At this point there is no scope
            // (companyId claim) available, so calling GetCompanyById() would trigger ScopeGuard and
            // return 403. We can safely return the newly created entity.
            // (Plan navigation may not be loaded here; that's OK for signup, which mainly needs the Id.)
            return CreatedAtAction(nameof(GetCompanyById), new { id = company.Id }, ToDto(company));
        }

        // PUT api/Companies/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCompany(int id, [FromBody] CreateCompanyRequest request)
        {
            var ok = await _companyService.UpdateCompany(request, id);
            if (!ok) return NotFound();

            var updated = await _companyService.GetCompanyById(id);
            if (updated == null) return NotFound();

            return Ok(ToDto(updated));
        }

        // DELETE api/Companies/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            var ok = await _companyService.DeleteCompany(id);
            if (!ok) return NotFound();
            return NoContent();
        }


        // GET api/Companies/{id}/initial-setup-status
        [HttpGet("{id:int}/initial-setup-status")]
        public async Task<IActionResult> GetInitialSetupStatus(int id)
        {
            var status = await _companyService.GetInitialSetupStatusAsync(id);
            if (!status.HasValue) return NotFound();

            return Ok(new CompanyInitialSetupStatusDTO
            {
                CompanyId = id,
                HasCompletedInitialSetup = status.Value
            });
        }

        // PUT api/Companies/{id}/initial-setup-status
        [HttpPut("{id:int}/initial-setup-status")]
        public async Task<IActionResult> UpdateInitialSetupStatus(int id, [FromBody] UpdateCompanyInitialSetupStatusRequest request)
        {
            var ok = await _companyService.SetInitialSetupStatusAsync(id, request.HasCompletedInitialSetup);
            if (!ok) return NotFound();

            return Ok(new CompanyInitialSetupStatusDTO
            {
                CompanyId = id,
                HasCompletedInitialSetup = request.HasCompletedInitialSetup
            });
        }

        // GET api/Companies/paged?Page=1&PageSize=10&Name=...&PlanId=...&Status=...
        [HttpGet("paged")]
        public async Task<IActionResult> GetCompaniesPaged([FromQuery] CompanyFiltersDTO filters)
        {
            var result = await _companyService.GetCompaniesPagedFilteredAsync(filters);

            return Ok(new
            {
                items = result.Results.Select(ToDto),
                totalCount = result.TotalItems,
                page = result.CurrentPage,
                pageSize = result.PageSize,
                pageCount = result.PageCount
            });
        }
    }
}
