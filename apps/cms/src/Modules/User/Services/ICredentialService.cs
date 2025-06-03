using cms.Modules.User.Models;

namespace cms.Modules.User.Services;

/// <summary>
/// Service interface for managing user credentials
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Get all credentials for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of credentials</returns>
    Task<IEnumerable<Credential>> GetCredentialsByUserIdAsync(Guid userId);

    /// <summary>
    /// Get a specific credential by ID
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>Credential or null if not found</returns>
    Task<Credential?> GetCredentialByIdAsync(Guid id);

    /// <summary>
    /// Get a credential by user ID and type
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="type">Credential type</param>
    /// <returns>Credential or null if not found</returns>
    Task<Credential?> GetCredentialByUserIdAndTypeAsync(Guid userId, string type);

    /// <summary>
    /// Create a new credential
    /// </summary>
    /// <param name="credential">Credential to create</param>
    /// <returns>Created credential</returns>
    Task<Credential> CreateCredentialAsync(Credential credential);

    /// <summary>
    /// Update an existing credential
    /// </summary>
    /// <param name="credential">Credential to update</param>
    /// <returns>Updated credential</returns>
    Task<Credential> UpdateCredentialAsync(Credential credential);

    /// <summary>
    /// Soft delete a credential
    /// </summary>
    /// <param name="id">Credential ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> SoftDeleteCredentialAsync(Guid id);

    /// <summary>
    /// Restore a soft-deleted credential
    /// </summary>
    /// <param name="id">Credential ID to restore</param>
    /// <returns>True if restored successfully</returns>
    Task<bool> RestoreCredentialAsync(Guid id);

    /// <summary>
    /// Permanently delete a credential
    /// </summary>
    /// <param name="id">Credential ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> HardDeleteCredentialAsync(Guid id);

    /// <summary>
    /// Mark a credential as used
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>True if marked successfully</returns>
    Task<bool> MarkCredentialAsUsedAsync(Guid id);

    /// <summary>
    /// Deactivate a credential
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>True if deactivated successfully</returns>
    Task<bool> DeactivateCredentialAsync(Guid id);

    /// <summary>
    /// Activate a credential
    /// </summary>
    /// <param name="id">Credential ID</param>
    /// <returns>True if activated successfully</returns>
    Task<bool> ActivateCredentialAsync(Guid id);

    /// <summary>
    /// Get all credentials including soft-deleted ones
    /// </summary>
    /// <returns>List of all credentials</returns>
    Task<IEnumerable<Credential>> GetAllCredentialsAsync();

    /// <summary>
    /// Get soft-deleted credentials
    /// </summary>
    /// <returns>List of soft-deleted credentials</returns>
    Task<IEnumerable<Credential>> GetDeletedCredentialsAsync();
}
