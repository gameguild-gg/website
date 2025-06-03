using cms.Models;
using HotChocolate.Types;

namespace cms.GraphQL;

public class UserType : ObjectType<User>
{
    protected override void Configure(IObjectTypeDescriptor<User> descriptor)
    {
        descriptor.Description("Represents a user in the CMS system.");
        
        descriptor
            .Field(u => u.Id)
            .Description("The unique identifier for the user.");
            
        descriptor
            .Field(u => u.Username)
            .Description("The username of the user.");
            
        descriptor
            .Field(u => u.Email)
            .Description("The email address of the user.");
            
        descriptor
            .Field(u => u.FirstName)
            .Description("The first name of the user.");
            
        descriptor
            .Field(u => u.LastName)
            .Description("The last name of the user.");
            
        descriptor
            .Field(u => u.IsActive)
            .Description("Indicates whether the user is active.");
            
        descriptor
            .Field(u => u.CreatedAt)
            .Description("The date and time when the user was created.");
            
        descriptor
            .Field(u => u.UpdatedAt)
            .Description("The date and time when the user was last updated.");
    }
}
