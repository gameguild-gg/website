using cms.Data;
using cms.Models;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace cms.GraphQL;

public class Mutation
{
    /// <summary>
    /// Creates a new user.
    /// </summary>
    public async Task<User> CreateUser(CreateUserInput input, ApplicationDbContext context)
    {
        // Check if username already exists
        var existingUsername = await context.Users
            .AnyAsync(u => u.Username == input.Username);
        if (existingUsername)
        {
            throw new GraphQLException($"Username '{input.Username}' already exists.");
        }
        
        // Check if email already exists
        var existingEmail = await context.Users
            .AnyAsync(u => u.Email == input.Email); 
        if (existingEmail)
        {
            throw new GraphQLException($"Email '{input.Email}' already exists.");
        }
        
        var user = new User
        {
            Username = input.Username,
            Email = input.Email,
            FirstName = input.FirstName,
            LastName = input.LastName,
            IsActive = input.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        return user;
    }
    
    /// <summary>
    /// Updates an existing user.
    /// </summary>
    public async Task<User> UpdateUser(UpdateUserInput input, ApplicationDbContext context)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == input.Id);
            
        if (user == null)
        {
            throw new GraphQLException($"User with ID '{input.Id}' not found.");
        }
        
        // Check if username is being changed and if it already exists
        if (!string.IsNullOrEmpty(input.Username) && input.Username != user.Username)
        {
            var existingUsername = await context.Users
                .AnyAsync(u => u.Username == input.Username && u.Id != input.Id);
            if (existingUsername)
            {
                throw new GraphQLException($"Username '{input.Username}' already exists.");
            }
            user.Username = input.Username;
        }
        
        // Check if email is being changed and if it already exists
        if (!string.IsNullOrEmpty(input.Email) && input.Email != user.Email)
        {
            var existingEmail = await context.Users
                .AnyAsync(u => u.Email == input.Email && u.Id != input.Id);
            if (existingEmail)
            {
                throw new GraphQLException($"Email '{input.Email}' already exists.");
            }
            user.Email = input.Email;
        }
        
        // Update other fields if provided
        if (!string.IsNullOrEmpty(input.FirstName))
            user.FirstName = input.FirstName;
            
        if (!string.IsNullOrEmpty(input.LastName))
            user.LastName = input.LastName;
            
        if (input.IsActive.HasValue)
            user.IsActive = input.IsActive.Value;
            
        user.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        return user;
    }
    
    /// <summary>
    /// Deletes a user by their ID.
    /// </summary>
    public async Task<bool> DeleteUser(Guid id, ApplicationDbContext context)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
            
        if (user == null)
        {
            throw new GraphQLException($"User with ID '{id}' not found.");
        }
        
        context.Users.Remove(user);
        await context.SaveChangesAsync();
        
        return true;
    }
    
    /// <summary>
    /// Toggles the active status of a user.
    /// </summary>
    public async Task<User> ToggleUserStatus(Guid id, ApplicationDbContext context)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id);
            
        if (user == null)
        {
            throw new GraphQLException($"User with ID '{id}' not found.");
        }
        
        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        return user;
    }
}
