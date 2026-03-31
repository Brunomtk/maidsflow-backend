using System.Threading.Tasks;

namespace Services.Storage
{
    public interface IS3StorageService
    {
        string? CreateDownloadUrl(string key, int? expiresMinutes = null);

        /// <summary>
        /// Tenta extrair a key do objeto no S3 a partir do valor salvo no banco.
        /// Preferencialmente salvamos apenas a key (ex.: "Checklists/...").
        /// Em casos legados, o valor pode ser uma URL do S3 (com ou sem querystring).
        /// </summary>
        bool TryGetKeyFromStoredValue(string storedValue, out string key);

        PresignedUploadResult CreateChecklistPhotoUploadUrl(int checklistId, int itemId, string fileName, string contentType);

        PresignedUploadResult CreateCompanyAvatarUploadUrl(int companyId, string fileName, string contentType);

        PresignedUploadResult CreateUserAvatarUploadUrl(int userId, string fileName, string contentType);

        PresignedUploadResult CreateHouseNotesPhotoUploadUrl(int customerId, int addressId, string fileName, string contentType);

        PresignedUploadResult CreateIssuePhotoUploadUrl(int appointmentId, int issueId, string fileName, string contentType);

        Task DeleteIfExistsAsync(string key);
    }
}
