using Core.DTO.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Infrastructure.Repositories;
using Services.Storage;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ControlApi.Controllers;

[ApiController]
[Route("api/Users/{userId:int}/avatar")]
public class UserAvatarsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IS3StorageService _s3;

    public UserAvatarsController(IUnitOfWork uow, IS3StorageService s3)
    {
        _uow = uow;
        _s3 = s3;
    }

    /// <summary>
    /// Generates a pre-signed PUT URL to upload the user's avatar directly to S3.
    /// </summary>
    [Authorize]
    [HttpPost("presign")]
    [ProducesResponseType(typeof(PresignUserAvatarResponse), 200)]
    public IActionResult Presign([FromRoute] int userId, [FromBody] PresignUserAvatarRequest req)
    {
        var fileName = string.IsNullOrWhiteSpace(req.FileName) ? "avatar" : req.FileName;
        var contentType = string.IsNullOrWhiteSpace(req.ContentType) ? "application/octet-stream" : req.ContentType;

        var presigned = _s3.CreateUserAvatarUploadUrl(userId, fileName, contentType);

        return Ok(new PresignUserAvatarResponse
        {
            UploadUrl = presigned.UploadUrl,
            Key = presigned.Key,
            // DTO expects DateTime; keep it in UTC.
            ExpiresAt = presigned.ExpiresAtUtc.UtcDateTime
        });
    }

    /// <summary>
    /// Confirms the upload and stores the S3 key in the User record.
    /// </summary>
    [Authorize]
    [HttpPut("confirm")]
    [ProducesResponseType(typeof(UserAvatarResponse), 200)]
    public async Task<IActionResult> Confirm([FromRoute] int userId, [FromBody] UpdateUserAvatarRequest req)
    {        var user = await _uow.Users.GetById(userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Delete previous avatar (best-effort)
        if (!string.IsNullOrWhiteSpace(user.AvatarKey))
        {
            var prevKey = user.AvatarKey;
            if (_s3.TryGetKeyFromStoredValue(prevKey!, out var normalizedPrevKey))
                prevKey = normalizedPrevKey;
            await _s3.DeleteIfExistsAsync(prevKey!);
        }

        user.AvatarKey = req.Key;
        user.UpdatedDate = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        var url = string.IsNullOrWhiteSpace(user.AvatarKey) ? null : _s3.CreateDownloadUrl(user.AvatarKey);

        return Ok(new UserAvatarResponse
        {
            AvatarKey = user.AvatarKey,
            AvatarUrl = url
        });
    }

    /// <summary>
    /// Returns a download URL for the current avatar.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(UserAvatarResponse), 200)]
    public async Task<IActionResult> Get([FromRoute] int userId)
    {
        var user = await _uow.Users.GetById(userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var url = string.IsNullOrWhiteSpace(user.AvatarKey) ? null : _s3.CreateDownloadUrl(user.AvatarKey);
        return Ok(new UserAvatarResponse
        {
            AvatarKey = user.AvatarKey,
            AvatarUrl = url
        });
    }

    /// <summary>
    /// Removes the avatar from S3 and clears the stored key.
    /// </summary>
    [Authorize]
    [HttpDelete]
    [ProducesResponseType(typeof(UserAvatarResponse), 200)]
    public async Task<IActionResult> Delete([FromRoute] int userId)
    {
        var user = await _uow.Users.GetById(userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (!string.IsNullOrWhiteSpace(user.AvatarKey))
        {
            var key = user.AvatarKey;
            if (_s3.TryGetKeyFromStoredValue(key!, out var normalized))
                key = normalized;
            await _s3.DeleteIfExistsAsync(key!);
        }

        user.AvatarKey = null;
        user.UpdatedDate = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        return Ok(new UserAvatarResponse
        {
            AvatarKey = null,
            AvatarUrl = null
        });
    }

    private bool IsSelfOrAdmin(int userId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        var nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(nameId, out var tokenUserId) && tokenUserId == userId)
            return true;

        return false;
    }
}
