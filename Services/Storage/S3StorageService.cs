using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Core.Options;
using Microsoft.Extensions.Options;

namespace Services.Storage;

public class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3;
    private readonly S3Options _opt;

    public S3StorageService(IOptions<S3Options> opt)
    {
        _opt = opt.Value ?? new S3Options();

        // Force Signature V4 (modern S3). Signature V2 commonly results in 403 for presigned PUT in modern accounts/buckets.
        AWSConfigsS3.UseSignatureVersion4 = true;

        var cfg = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_opt.Region ?? "us-east-1"),
            SignatureVersion = "4"
        };

        // Prefer IAM role / environment variables. Explicit creds are optional.
        if (!string.IsNullOrWhiteSpace(_opt.AccessKeyId) && !string.IsNullOrWhiteSpace(_opt.SecretAccessKey))
        {
            var creds = new BasicAWSCredentials(_opt.AccessKeyId, _opt.SecretAccessKey);
            _s3 = new AmazonS3Client(creds, cfg);
        }
        else
        {
            _s3 = new AmazonS3Client(cfg);
        }
    }

    public PresignedUploadResult CreateChecklistPhotoUploadUrl(int checklistId, int itemId, string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(_opt.BucketName))
            throw new InvalidOperationException("S3 BucketName não configurado. Configure em appsettings (S3:BucketName) ou variável de ambiente.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        var key = BuildChecklistPhotoKey(checklistId, itemId, ext);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_opt.UploadUrlExpiresMinutes <= 0 ? 10 : _opt.UploadUrlExpiresMinutes);

        var req = new GetPreSignedUrlRequest
        {
            BucketName = _opt.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Protocol = Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime
            // NOTE: Do NOT set ContentType here. Browsers may send a Content-Type that doesn't match exactly,
            // which can break presigned URLs. Keep it flexible.
        };

        var url = _s3.GetPreSignedURL(req);
        return new PresignedUploadResult(key, url, expiresAt);
    }

    public PresignedUploadResult CreateCompanyAvatarUploadUrl(int companyId, string fileName, string contentType)
    {
        if (companyId <= 0) throw new ArgumentOutOfRangeException(nameof(companyId));
        if (string.IsNullOrWhiteSpace(_opt.BucketName))
            throw new InvalidOperationException("S3 BucketName não configurado. Configure em appsettings (S3:BucketName) ou variável de ambiente.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        var key = BuildCompanyAvatarKey(companyId, ext);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_opt.UploadUrlExpiresMinutes <= 0 ? 10 : _opt.UploadUrlExpiresMinutes);

        var req = new GetPreSignedUrlRequest
        {
            BucketName = _opt.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Protocol = Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime
        };

        var url = _s3.GetPreSignedURL(req);
        return new PresignedUploadResult(key, url, expiresAt);
    }

    public string? CreateDownloadUrl(string key, int? expiresMinutes = null)
    {
        if (string.IsNullOrWhiteSpace(_opt.BucketName)) return key;
        if (string.IsNullOrWhiteSpace(key)) return key;

        var minutes = expiresMinutes ?? (_opt.DownloadUrlExpiresMinutes <= 0 ? 60 : _opt.DownloadUrlExpiresMinutes);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(minutes);

        var req = new GetPreSignedUrlRequest
        {
            BucketName = _opt.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime
        };

        return _s3.GetPreSignedURL(req);
    }

    public bool TryGetKeyFromStoredValue(string storedValue, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(storedValue)) return false;

        // If it's already a key (no scheme), assume it's the S3 key.
        if (!storedValue.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            !storedValue.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            key = storedValue.TrimStart('/');
            return true;
        }

        // s3://bucket/key
        if (storedValue.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var noScheme = storedValue.Substring("s3://".Length);
            var firstSlash = noScheme.IndexOf('/');
            if (firstSlash <= 0) return false;
            key = noScheme[(firstSlash + 1)..];
            return !string.IsNullOrWhiteSpace(key);
        }

        // https://bucket.s3.region.amazonaws.com/key OR https://s3.region.amazonaws.com/bucket/key
        try
        {
            var uri = new Uri(storedValue);
            var path = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrWhiteSpace(path)) return false;

            // If path starts with bucket name, strip it.
            if (!string.IsNullOrWhiteSpace(_opt.BucketName) &&
                path.StartsWith(_opt.BucketName + "/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(_opt.BucketName.Length + 1);

            key = path;
            return !string.IsNullOrWhiteSpace(key);
        }
        catch
        {
            return false;
        }
    }

    public async Task DeleteIfExistsAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(_opt.BucketName)) return;
        if (string.IsNullOrWhiteSpace(key)) return;

        try
        {
            await _s3.DeleteObjectAsync(_opt.BucketName, key);
        }
        catch
        {
            // swallow: deleting is best-effort
        }
    }

    private string BuildChecklistPhotoKey(int checklistId, int itemId, string ext)
    {
        var prefix = string.IsNullOrWhiteSpace(_opt.ChecklistPrefix) ? "Checklists/" : _opt.ChecklistPrefix;
        if (!prefix.EndsWith('/')) prefix += "/";

        var name = $"{Guid.NewGuid():N}{ext}";
        return $"{prefix}{checklistId}/items/{itemId}/{name}";
    }

    private string BuildCompanyAvatarKey(int companyId, string ext)
    {
        var prefix = string.IsNullOrWhiteSpace(_opt.CompanyAvatarPrefix) ? "AvatarCompany/" : _opt.CompanyAvatarPrefix;
        if (!prefix.EndsWith('/')) prefix += "/";

        var name = $"{Guid.NewGuid():N}{ext}";
        return $"{prefix}{companyId}/{name}";
    }
}
