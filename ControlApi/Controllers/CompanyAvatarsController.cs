using System;
using System.Threading.Tasks;

using Core.DTO.Company;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Storage;

namespace ControlApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Companies/{companyId:int}/avatar")]
    public class CompanyAvatarsController : ControllerBase
    {
        private readonly DbContextClass _db;
        private readonly IS3StorageService _s3;

        public CompanyAvatarsController(DbContextClass db, IS3StorageService s3)
        {
            _db = db;
            _s3 = s3;
        }

        /// <summary>
        /// Generates a presigned PUT URL to upload an avatar image to S3.
        /// After uploading to S3, call PUT /api/Companies/{companyId}/avatar to persist the Key into the company.
        /// </summary>
        [HttpPost("presign")]
        public async Task<IActionResult> PresignUpload(int companyId, [FromBody] PresignCompanyAvatarRequest request)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) return NotFound();

            var presign = _s3.CreateCompanyAvatarUploadUrl(companyId, request.FileName, request.ContentType);

            return Ok(new PresignCompanyAvatarResponse
            {
                Key = presign.Key,
                UploadUrl = presign.UploadUrl,
                ExpiresAtUtc = presign.ExpiresAtUtc.ToString("O"),
                DownloadUrl = _s3.CreateDownloadUrl(presign.Key)
            });
        }

        /// <summary>
        /// Persists the avatar S3 key into the Company record.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> SetAvatar(int companyId, [FromBody] UpdateCompanyAvatarRequest request)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) return NotFound();

            company.AvatarKey = request.Key;
            company.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                avatarKey = company.AvatarKey,
                avatarUrl = string.IsNullOrWhiteSpace(company.AvatarKey) ? null : _s3.CreateDownloadUrl(company.AvatarKey)
            });
        }

        /// <summary>
        /// Returns current avatar info (key + presigned download URL).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvatar(int companyId)
        {
            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) return NotFound();

            return Ok(new
            {
                avatarKey = company.AvatarKey,
                avatarUrl = string.IsNullOrWhiteSpace(company.AvatarKey) ? null : _s3.CreateDownloadUrl(company.AvatarKey)
            });
        }

        /// <summary>
        /// Clears the avatar from the Company and removes the object from S3 (best-effort).
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteAvatar(int companyId)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(company.AvatarKey))
            {
                await _s3.DeleteIfExistsAsync(company.AvatarKey);
            }

            company.AvatarKey = null;
            company.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
