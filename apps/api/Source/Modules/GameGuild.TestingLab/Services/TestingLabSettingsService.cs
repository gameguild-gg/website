using GameGuild.Identity.Tenants;


namespace GameGuild.TestingLab;

/// <summary> Implementation of the TestingLabSettings service </summary>
public class TestingLabSettingsService : ITestingLabSettingsService {
  private readonly IApplicationDbContext _dbContext;

  public TestingLabSettingsService(IApplicationDbContext dbContext) { _dbContext = dbContext; }

  /// <inheritdoc />
  public async Task<TestingLabSettings> GetTestingLabSettingsAsync(Guid? tenantId = null) {
    var settings = await _dbContext.Set<TestingLabSettings>().Include(s => s.Tenant).FirstOrDefaultAsync(s => tenantId == null ? s.Tenant == null : s.Tenant != null && s.Tenant.Id == tenantId).ConfigureAwait(false);

    if (settings == null) {
      // Create default settings
      settings = new TestingLabSettings {
        Tenant = tenantId.HasValue ? await _dbContext.Set<Tenant>().FindAsync(tenantId.Value).ConfigureAwait(false) : null,
        LabName = "Testing Lab",
        Description = "Default testing lab settings",
        Timezone = "UTC",
        DefaultSessionDuration = 60,
        AllowPublicSignups = true,
        RequireApproval = true,
        EnableNotifications = true,
        MaxSimultaneousSessions = 10,
        VersionSubmissionPolicy = GameGuild.Projects.VersionSubmissionPolicy.ReadyMutableUntilReview,
      };

      _dbContext.Set<TestingLabSettings>().Add(settings);
      await _dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    return settings;
  }

  /// <inheritdoc />
  public async Task<TestingLabSettingsDto> GetTestingLabSettingsDtoAsync(Guid? tenantId = null) {
    var settings = await GetTestingLabSettingsAsync(tenantId).ConfigureAwait(false);

    return MapToDto(settings);
  }

  /// <inheritdoc />
  public async Task<TestingLabSettings> CreateOrUpdateTestingLabSettingsAsync(Guid? tenantId, CreateTestingLabSettingsDto dto) {
    ArgumentNullException.ThrowIfNull(dto);

    var settings = await _dbContext.Set<TestingLabSettings>().Include(s => s.Tenant).FirstOrDefaultAsync(s => tenantId == null ? s.Tenant == null : s.Tenant != null && s.Tenant.Id == tenantId).ConfigureAwait(false);

    if (settings == null) {
      // Create new settings
      settings = new TestingLabSettings { Tenant = tenantId.HasValue ? await _dbContext.Set<Tenant>().FindAsync(tenantId.Value).ConfigureAwait(false) : null };
      _dbContext.Set<TestingLabSettings>().Add(settings);
    }

    // Update properties
    settings.LabName = dto.LabName;
    settings.Description = dto.Description;
    settings.Timezone = dto.Timezone;
    settings.DefaultSessionDuration = dto.DefaultSessionDuration;
    settings.AllowPublicSignups = dto.AllowPublicSignups;
    settings.RequireApproval = dto.RequireApproval;
    settings.EnableNotifications = dto.EnableNotifications;
    settings.MaxSimultaneousSessions = dto.MaxSimultaneousSessions;
    settings.ReminderDaysBefore = dto.ReminderDaysBefore;
    settings.VersionSubmissionPolicy = dto.VersionSubmissionPolicy;

    await _dbContext.SaveChangesAsync().ConfigureAwait(false);

    return settings;
  }

  /// <inheritdoc />
  public async Task<TestingLabSettings> UpdateTestingLabSettingsAsync(Guid? tenantId, UpdateTestingLabSettingsDto dto) {
    ArgumentNullException.ThrowIfNull(dto);

    var settings = await GetTestingLabSettingsAsync(tenantId).ConfigureAwait(false);

    // Update only non-null properties
    if (dto.LabName != null)
        {
            settings.LabName = dto.LabName;
        }

        if (dto.Description != null)
        {
            settings.Description = dto.Description;
        }

        if (dto.Timezone != null)
        {
            settings.Timezone = dto.Timezone;
        }

        if (dto.DefaultSessionDuration.HasValue)
        {
            settings.DefaultSessionDuration = dto.DefaultSessionDuration.Value;
        }

        if (dto.AllowPublicSignups.HasValue)
        {
            settings.AllowPublicSignups = dto.AllowPublicSignups.Value;
        }

        if (dto.RequireApproval.HasValue)
        {
            settings.RequireApproval = dto.RequireApproval.Value;
        }

        if (dto.EnableNotifications.HasValue)
        {
            settings.EnableNotifications = dto.EnableNotifications.Value;
        }

        if (dto.MaxSimultaneousSessions.HasValue)
        {
            settings.MaxSimultaneousSessions = dto.MaxSimultaneousSessions.Value;
        }

        if (dto.VersionSubmissionPolicy.HasValue)
        {
            settings.VersionSubmissionPolicy = dto.VersionSubmissionPolicy.Value;
        }

        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

    return settings;
  }

  /// <inheritdoc />
  public async Task<TestingLabSettings> ResetTestingLabSettingsAsync(Guid? tenantId = null) {
    var settings = await _dbContext.Set<TestingLabSettings>().Include(s => s.Tenant).FirstOrDefaultAsync(s => tenantId == null ? s.Tenant == null : s.Tenant != null && s.Tenant.Id == tenantId).ConfigureAwait(false);

    if (settings != null) {
      // Reset to defaults
      settings.LabName = "Testing Lab";
      settings.Description = "Default testing lab settings";
      settings.Timezone = "UTC";
      settings.DefaultSessionDuration = 60;
      settings.AllowPublicSignups = true;
      settings.RequireApproval = true;
      settings.EnableNotifications = true;
      settings.MaxSimultaneousSessions = 10;
      settings.VersionSubmissionPolicy = GameGuild.Projects.VersionSubmissionPolicy.ReadyMutableUntilReview;

      await _dbContext.SaveChangesAsync().ConfigureAwait(false);

      return settings;
    }

    // If no settings exist, create default ones
    return await GetTestingLabSettingsAsync(tenantId).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<bool> TestingLabSettingsExistAsync(Guid? tenantId = null) {
    return await _dbContext.Set<TestingLabSettings>().Include(s => s.Tenant).AnyAsync(s => tenantId == null ? s.Tenant == null : s.Tenant != null && s.Tenant.Id == tenantId).ConfigureAwait(false);
  }

  // Helper method to map entity to DTO
  private static TestingLabSettingsDto MapToDto(TestingLabSettings settings) {
    return new TestingLabSettingsDto {
      Id = settings.Id,
      LabName = settings.LabName,
      Description = settings.Description,
      Timezone = settings.Timezone,
      DefaultSessionDuration = settings.DefaultSessionDuration,
      AllowPublicSignups = settings.AllowPublicSignups,
      RequireApproval = settings.RequireApproval,
      EnableNotifications = settings.EnableNotifications,
      ReminderDaysBefore = settings.ReminderDaysBefore,
      MaxSimultaneousSessions = settings.MaxSimultaneousSessions,
      VersionSubmissionPolicy = settings.VersionSubmissionPolicy,
      TenantId = settings.Tenant?.Id,
      CreatedAt = settings.CreatedAt,
      UpdatedAt = settings.UpdatedAt,
    };
  }
}
