using cms.Modules.User.Models;

namespace cms.Modules.User.GraphQL;

/// <summary>
/// GraphQL type definition for Credential entity
/// </summary>
public class CredentialType : ObjectType<Credential>
{
    protected override void Configure(IObjectTypeDescriptor<Credential> descriptor)
    {
        descriptor.Description("Represents a user credential (password, API key, OAuth token, etc.)");

        descriptor.Field(c => c.Id)
            .Description("Unique identifier for the credential");

        descriptor.Field(c => c.UserId)
            .Description("Foreign key to the User entity");

        descriptor.Field(c => c.User)
            .Description("Navigation property to the User entity");

        descriptor.Field(c => c.Type)
            .Description("Type of credential (e.g., password, api_key, oauth_token, 2fa_secret)");

        descriptor.Field(c => c.Value)
            .Description("The credential value (redacted for security)")
            .Resolve(context => "***REDACTED***"); // Don't expose actual credential values

        descriptor.Field(c => c.Metadata)
            .Description("Additional metadata for the credential (JSON format)");

        descriptor.Field(c => c.ExpiresAt)
            .Description("When this credential expires (optional)");

        descriptor.Field(c => c.IsActive)
            .Description("Whether this credential is currently active");

        descriptor.Field(c => c.LastUsedAt)
            .Description("When this credential was last used");

        descriptor.Field(c => c.IsExpired)
            .Description("Check if the credential is expired");

        descriptor.Field(c => c.IsValid)
            .Description("Check if the credential is valid (active and not expired)");

        // Base entity properties
        descriptor.Field(c => c.Version)
            .Description("Version number for optimistic concurrency control");

        descriptor.Field(c => c.CreatedAt)
            .Description("When the credential was created");

        descriptor.Field(c => c.UpdatedAt)
            .Description("When the credential was last updated");

        descriptor.Field(c => c.DeletedAt)
            .Description("When the credential was soft deleted (null if not deleted)");

        descriptor.Field(c => c.IsDeleted)
            .Description("Whether the credential is soft deleted");
    }
}
