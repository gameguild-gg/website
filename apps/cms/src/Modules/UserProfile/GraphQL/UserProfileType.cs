namespace GameGuild.Modules.UserProfile.GraphQL;

/// <summary>
/// GraphQL type for UserProfile entity
/// </summary>
public class UserProfileType : ObjectType<Models.UserProfile>
{
    protected override void Configure(IObjectTypeDescriptor<Models.UserProfile> descriptor)
    {
        descriptor.Description("Represents a user profile with personal information and settings");

        // Base entity fields from ResourceBase
        descriptor
            .Field(f => f.Id)
            .Description("The unique identifier for the user profile");

        descriptor
            .Field(f => f.Version)
            .Description("The version number for optimistic concurrency control");

        descriptor
            .Field(f => f.CreatedAt)
            .Description("The date and time when the user profile was created");

        descriptor
            .Field(f => f.UpdatedAt)
            .Description("The date and time when the user profile was last updated");

        descriptor
            .Field(f => f.DeletedAt)
            .Description("The date and time when the user profile was soft deleted");

        descriptor
            .Field(f => f.IsDeleted)
            .Description("Indicates whether the user profile has been soft deleted");

        descriptor
            .Field(f => f.Owner)
            .Description("The user who owns this profile");

        // ResourceBase fields
        descriptor
            .Field(f => f.Title)
            .Description("The title of the user profile");

        descriptor
            .Field(f => f.Description)
            .Description("A description of the user profile");


        descriptor
            .Field(f => f.Tenant)
            .Description("The tenant this profile belongs to (null for global profiles)");

        descriptor
            .Field(f => f.Visibility)
            .Description("The visibility status of the user profile");

        // UserProfile-specific fields
        descriptor
            .Field(f => f.GivenName)
            .Description("The user's given (first) name");

        descriptor
            .Field(f => f.FamilyName)
            .Description("The user's family (last) name");

        descriptor
            .Field(f => f.DisplayName)
            .Description("The user's preferred display name");

        // Navigation properties
        descriptor
            .Field(f => f.Metadata)
            .Description("Metadata associated with this user profile resource");

        descriptor
            .Field(f => f.Localizations)
            .Description("Localized versions of this user profile");
    }
}
