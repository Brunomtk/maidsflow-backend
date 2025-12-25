using System;

namespace Services.Storage;

public record PresignedUploadResult(string Key, string UploadUrl, DateTimeOffset ExpiresAt);

public interface IS3StorageService
{
    PresignedUploadResult CreateChecklistPhotoUploadUrl(int checklistId, int itemId, string fileName, string contentType);
    string CreateDownloadUrl(string key);
    bool TryGetKeyFromStoredValue(string storedValue, out string key);
    System.Threading.Tasks.Task DeleteIfExistsAsync(string key);
}
