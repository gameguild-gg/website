using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;
using cms.Data;
using cms.Modules.Reputation.Models;

namespace cms.Modules.Reputation.Services;

/// <summary>
/// Service implementation for managing user reputation with polymorphic support
/// </summary>
public class ReputationService : IReputationService
{
    private readonly ApplicationDbContext _context;


    public ReputationService(ApplicationDbContext context)
    {
        _context = context;

    }

    public async Task<IReputation?> GetUserReputationAsync(Guid userId, Guid? tenantId = null)
    {
        if (tenantId.HasValue)
        {
            // Get tenant-specific reputation
            var userTenant = await _context.UserTenants
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId.Value && !ut.IsDeleted);

            if (userTenant == null)
                return null;

            return await _context.UserTenantReputations
                .Include(utr => utr.CurrentLevel)
                .FirstOrDefaultAsync(utr => utr.UserTenantId == userTenant.Id && !utr.IsDeleted);
        }
        else
        {
            // Get global reputation
            return await _context.UserReputations
                .Include(ur => ur.CurrentLevel)
                .FirstOrDefaultAsync(ur => ur.UserId == userId && !ur.IsDeleted);
        }
    }

    public async Task<IReputation> UpdateReputationAsync(Guid userId, int scoreChange, Guid? tenantId = null, string? reason = null)
    {
        var reputation = await GetUserReputationAsync(userId, tenantId);

        if (reputation == null)
        {
            // Create new reputation record
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                throw new ArgumentException("User not found", nameof(userId));

            if (tenantId.HasValue)
            {
                // Create tenant-specific reputation
                var userTenant = await _context.UserTenants
                    .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId.Value && !ut.IsDeleted);

                if (userTenant == null)
                    throw new ArgumentException("User is not a member of the specified tenant", nameof(tenantId));

                var tenantReputation = new UserTenantReputation
                {
                    UserTenant = userTenant,
                    Score = Math.Max(0, scoreChange), // Don't allow negative starting scores
                    LastUpdated = DateTime.UtcNow,
                    Title = $"Reputation for {user.Name} in {userTenant.Tenant?.Name ?? "Tenant"}"
                };

                _context.UserTenantReputations.Add(tenantReputation);
                reputation = tenantReputation;
            }
            else
            {
                // Create global reputation
                var globalReputation = new UserReputation
                {
                    User = user,
                    Score = Math.Max(0, scoreChange), // Don't allow negative starting scores
                    LastUpdated = DateTime.UtcNow,
                    Title = $"Global Reputation for {user.Name}"
                };

                _context.UserReputations.Add(globalReputation);
                reputation = globalReputation;
            }
        }
        else
        {
            // Update existing reputation
            reputation.Score = Math.Max(0, reputation.Score + scoreChange); // Don't allow negative scores
            reputation.LastUpdated = DateTime.UtcNow;
            reputation.Touch();
        }

        // Recalculate reputation level
        await RecalculateReputationLevelAsync(reputation, tenantId);

        // Record history entry
        CreateHistoryEntry(reputation, scoreChange, reason ?? "Manual adjustment", tenantId);

        await _context.SaveChangesAsync();

        return reputation;
    }

    private async Task RecalculateReputationLevelAsync(IReputation reputation, Guid? tenantId)
    {
        var newLevel = await _context.ReputationLevels
            .Where(rl => rl.IsActive &&
                         !rl.IsDeleted &&
                         rl.MinimumScore <= reputation.Score &&
                         (rl.MaximumScore == null || rl.MaximumScore >= reputation.Score)
            )
            .OrderByDescending(rl => rl.MinimumScore)
            .FirstOrDefaultAsync();

        if (newLevel?.Id != reputation.CurrentLevelId)
        {
            reputation.CurrentLevel = newLevel;
            reputation.CurrentLevelId = newLevel?.Id;
            reputation.LastLevelCalculation = DateTime.UtcNow;
        }
    }

    private void CreateHistoryEntry(IReputation reputation, int scoreChange, string reason, Guid? tenantId)
    {
        var historyEntry = new UserReputationHistory
        {
            PointsChange = scoreChange,
            PreviousScore = reputation.Score - scoreChange,
            NewScore = reputation.Score,
            Reason = reason,
            OccurredAt = DateTime.UtcNow,
            Title = $"Reputation change: {scoreChange:+#;-#;0}"
        };

        // Set the appropriate foreign key based on reputation type
        if (reputation is UserReputation userRep)
        {
            historyEntry.UserId = userRep.UserId;
        }
        else if (reputation is UserTenantReputation tenantRep)
        {
            historyEntry.UserTenantId = tenantRep.UserTenantId;
        }

        _context.UserReputationHistory.Add(historyEntry);
    }

    public async Task<IEnumerable<IReputation>> GetUsersByReputationLevelAsync(ReputationLevel minimumLevel, Guid? tenantId = null)
    {
        var results = new List<IReputation>();

        if (tenantId.HasValue)
        {
            // Get tenant-specific reputations
            var tenantReputations = await _context.UserTenantReputations
                .Include(utr => utr.CurrentLevel)
                .Include(utr => utr.UserTenant)
                .ThenInclude(ut => ut.User)
                .Where(utr => utr.Score >= minimumLevel.MinimumScore &&
                              utr.UserTenant.TenantId == tenantId.Value &&
                              !utr.IsDeleted
                )
                .OrderByDescending(utr => utr.Score)
                .ToListAsync();

            results.AddRange(tenantReputations);
        }
        else
        {
            // Get global reputations
            var globalReputations = await _context.UserReputations
                .Include(ur => ur.CurrentLevel)
                .Include(ur => ur.User)
                .Where(ur => ur.Score >= minimumLevel.MinimumScore && !ur.IsDeleted)
                .OrderByDescending(ur => ur.Score)
                .ToListAsync();

            results.AddRange(globalReputations);
        }

        return results;
    }


    // Additional helper methods for backward compatibility and extended functionality
    public async Task<IEnumerable<UserReputationHistory>> GetUserReputationHistoryAsync(Guid userId, Guid? tenantId = null, int limit = 50)
    {
        var query = _context.UserReputationHistory
            .Include(h => h.ReputationAction)
            .Include(h => h.RelatedResource)
            .Include(h => h.TriggeredByUser)
            .Include(h => h.PreviousLevel)
            .Include(h => h.NewLevel)
            .Where(h => !h.IsDeleted);

        if (tenantId.HasValue)
        {
            query = query.Where(h => h.UserTenantId != null &&
                                     h.UserTenant!.UserId == userId &&
                                     h.UserTenant.TenantId == tenantId.Value
            );
        }
        else
        {
            query = query.Where(h => h.UserId == userId);
        }

        return await query
            .OrderByDescending(h => h.OccurredAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<IReputation>> GetTopUsersAsync(Guid? tenantId = null, int limit = 10)
    {
        var results = new List<IReputation>();

        if (tenantId.HasValue)
        {
            var tenantReputations = await _context.UserTenantReputations
                .Include(utr => utr.CurrentLevel)
                .Include(utr => utr.UserTenant)
                .ThenInclude(ut => ut.User)
                .Where(utr => utr.UserTenant.TenantId == tenantId.Value && !utr.IsDeleted)
                .OrderByDescending(utr => utr.Score)
                .Take(limit)
                .ToListAsync();

            results.AddRange(tenantReputations);
        }
        else
        {
            var globalReputations = await _context.UserReputations
                .Include(ur => ur.CurrentLevel)
                .Include(ur => ur.User)
                .Where(ur => !ur.IsDeleted)
                .OrderByDescending(ur => ur.Score)
                .Take(limit)
                .ToListAsync();

            results.AddRange(globalReputations);
        }

        return results;
    }
}
