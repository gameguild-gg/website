/**
 * @game-guild/client - UsersPreferences Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersPreferencesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get user preferences
   */
  async getUsersPreferences(userId: string): Promise<Result<Types.IdentityUsersUserPreferencesDto, ApiError>> {
    const url = `/v1/users/${userId}/preferences`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserPreferencesDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace user preferences by user ID
   */
  async putUsersPreferences(userId: string, body: Types.IdentityUsersReplaceUserPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersReplaceUserPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update user preferences by user ID
   */
  async patchUsersPreferences(userId: string, body: Types.IdentityUsersUpdateUserPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reset user preferences to defaults
   */
  async postUsersPreferencesReset(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get accessibility settings for user
   */
  async getUsersPreferencesAccessibility(userId: string): Promise<Result<Types.IdentityUsersUserAccessibilityPreferencesDto, ApiError>> {
    const url = `/v1/users/${userId}/preferences/accessibility`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserAccessibilityPreferencesDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace accessibility preferences for user (full update)
   */
  async putUsersPreferencesAccessibility(userId: string, body: Types.IdentityUsersReplaceUserAccessibilityPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/accessibility`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersReplaceUserAccessibilityPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update accessibility preferences for user
   */
  async patchUsersPreferencesAccessibility(userId: string, body: Types.IdentityUsersUpdateUserAccessibilityPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/accessibility`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserAccessibilityPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if accessibility preferences exist
   */
  async headUsersPreferencesAccessibility(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/accessibility`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reset accessibility preferences to defaults
   */
  async postUsersPreferencesAccessibilityReset(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/accessibility:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get localization settings for user
   */
  async getUsersPreferencesLocalization(userId: string): Promise<Result<Types.IdentityUsersUserLocalizationPreferencesDto, ApiError>> {
    const url = `/v1/users/${userId}/preferences/localization`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserLocalizationPreferencesDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace localization preferences for user (full update)
   */
  async putUsersPreferencesLocalization(userId: string, body: Types.IdentityUsersReplaceUserLocalizationPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/localization`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersReplaceUserLocalizationPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update localization preferences for user
   */
  async patchUsersPreferencesLocalization(userId: string, body: Types.IdentityUsersUpdateUserLocalizationPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/localization`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserLocalizationPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if localization preferences exist
   */
  async headUsersPreferencesLocalization(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/localization`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reset localization preferences to defaults
   */
  async postUsersPreferencesLocalizationReset(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/localization:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get notification settings for user
   */
  async getUsersPreferencesNotifications(userId: string): Promise<Result<Types.IdentityUsersUserNotificationPreferencesDto, ApiError>> {
    const url = `/v1/users/${userId}/preferences/notifications`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserNotificationPreferencesDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace notification preferences for user (full update)
   */
  async putUsersPreferencesNotifications(userId: string, body: Types.IdentityUsersReplaceUserNotificationPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/notifications`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersReplaceUserNotificationPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update notification preferences for user
   */
  async patchUsersPreferencesNotifications(userId: string, body: Types.IdentityUsersUpdateUserNotificationPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/notifications`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserNotificationPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if notification preferences exist
   */
  async headUsersPreferencesNotifications(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/notifications`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reset notification preferences to defaults
   */
  async postUsersPreferencesNotificationsReset(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/notifications:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get privacy settings for user
   */
  async getUsersPreferencesPrivacy(userId: string): Promise<Result<Types.IdentityUsersUserPrivacyPreferencesDto, ApiError>> {
    const url = `/v1/users/${userId}/preferences/privacy`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserPrivacyPreferencesDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace privacy preferences for user (full update)
   */
  async putUsersPreferencesPrivacy(userId: string, body: Types.IdentityUsersReplaceUserPrivacyPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/privacy`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersReplaceUserPrivacyPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update privacy preferences for user
   */
  async patchUsersPreferencesPrivacy(userId: string, body: Types.IdentityUsersUpdateUserPrivacyPreferencesInput): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/privacy`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserPrivacyPreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if privacy preferences exist
   */
  async headUsersPreferencesPrivacy(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/privacy`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reset privacy preferences to defaults
   */
  async postUsersPreferencesPrivacyReset(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}/preferences/privacy:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createUsersPreferencesModule(client: ApiClient): UsersPreferencesModule {
  return new UsersPreferencesModule(client);
}
