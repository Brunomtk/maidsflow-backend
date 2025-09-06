using Microsoft.AspNetCore.Mvc;
using Core.DTO.Company;
using Core.Models;
using Services;
using Core.Enums;
using System.Linq;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompaniesController : ControllerBase // 🔥 nome plural aqui
    {
        private readonly ICompanyService _companyService;

        public CompaniesController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var companies = await _companyService.GetAllCompanies();
            return Ok(companies);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] CompanyFiltersDTO filters)
        {
            var paged = await _companyService.GetCompaniesPagedFilteredAsync(filters);

            var result = new CompanyPagedDTO
            {
                PageCount = paged.PageCount,
                Result = paged.Results.Select(c => new CompanyDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Cnpj = c.Cnpj,
                    Responsible = c.Responsible,
                    Email = c.Email,
                    Phone = c.Phone,
                    PlanId = c.PlanId,
                    PlanName = c.Plan?.Name,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate
                }).ToList()
            };

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var company = await _companyService.GetCompanyById(id);
            if (company == null) return NotFound("Company not found.");
            return Ok(company);
        }

        [HttpGet("{companyId}/plan-id")]
        public async Task<IActionResult> GetPlanIdByCompanyId(int companyId)
        {
            var planId = await _companyService.GetPlanIdByCompanyId(companyId);
            return planId.HasValue ? Ok(planId.Value) : NotFound("Company not found.");
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request)
        {
            var company = new Company
            {
                Name = request.Name,
                Cnpj = request.Cnpj,
                Responsible = request.Responsible,
                Email = request.Email,
                Phone = request.Phone,
                PlanId = request.PlanId,
                Status = request.Status
            };

            var created = await _companyService.CreateCompany(company);
            return created ? Ok("Company created successfully.") : BadRequest("Error creating company.");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateCompanyRequest request)
        {
            var updated = await _companyService.UpdateCompany(request, id);
            return updated ? Ok("Company updated successfully.") : NotFound("Company not found.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _companyService.DeleteCompany(id);
            return deleted ? Ok("Company deleted successfully.") : NotFound("Company not found.");
        }
    }
}
