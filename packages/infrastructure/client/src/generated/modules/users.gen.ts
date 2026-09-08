/**
 * @game-guild/client - Users Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class UsersModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get users with pagination, search, and sorting
   *
   * Retrieves a paginated list of users with optional filtering by email, status, and text search.
   */
  async getUsersForGetUsers(query?: {
    email?: string;
    status?: string;
    includeDeleted?: boolean;
    q?: string;
    cursor?: string;
    limit?: number;
    sort?: string;
  }): Promise<Result<Types.PagedResultUserDto, ApiError>> {
    const url = '/v1/users';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a new user
   *
   * Creates a new user account with the provided information.
   */
  async postUsers(body: Types.IdentityUsersCreateUserInput): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = '/v1/users';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersCreateUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk activate user accounts
   *
   * Activates multiple user accounts at once.
   */
  async postUsersActivateForPostUsersActivate(
    body: Types.IdentityUsersBulkActivateUsersInput,
  ): Promise<Result<Types.IdentityUsersBulkActivateUsersOutput, ApiError>> {
    const url = '/v1/users:activate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkActivateUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersBulkActivateUsersOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk create users
   *
   * Creates multiple user accounts at once.
   */
  async postUsersCreate(body: Types.IdentityUsersBulkCreateUsersInput): Promise<Result<Types.IdentityUsersBulkCreateUsersOutput, ApiError>> {
    const url = '/v1/users:create';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkCreateUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersBulkCreateUsersOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk deactivate user accounts
   *
   * Deactivates multiple user accounts at once.
   */
  async postUsersDeactivateForPostUsersDeactivate(
    body: Types.IdentityUsersBulkDeactivateUsersInput,
  ): Promise<Result<Types.IdentityUsersBulkDeactivateUsersOutput, ApiError>> {
    const url = '/v1/users:deactivate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkDeactivateUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersBulkDeactivateUsersOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk soft delete users
   *
   * Soft deletes multiple users at once.
   */
  async postUsersDelete(body: Types.IdentityUsersBulkDeleteUsersInput): Promise<Result<void, ApiError>> {
    const url = '/v1/users:delete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkDeleteUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Bulk hard delete users (irreversible purge)
   *
   * Permanently deletes multiple users. Admin operation requiring proper authorization.
   */
  async postUsersPurgeForPostUsersPurge(body: Types.IdentityUsersBulkPurgeUsersInput): Promise<Result<void, ApiError>> {
    const url = '/v1/users:purge';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkPurgeUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Bulk full update users
   *
   * Updates multiple users with complete data.
   */
  async postUsersReplace(body: Types.IdentityUsersBulkUpdateUsersInput): Promise<Result<void, ApiError>> {
    const url = '/v1/users:replace';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkUpdateUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Bulk suspend user accounts
   *
   * Suspends multiple user accounts at once.
   */
  async postUsersSuspendForPostUsersSuspend(
    body: Types.IdentityUsersBulkSuspendUsersInput,
  ): Promise<Result<Types.IdentityUsersBulkSuspendUsersOutput, ApiError>> {
    const url = '/v1/users:suspend';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkSuspendUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersBulkSuspendUsersOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk undelete soft-deleted users
   *
   * Restores multiple soft-deleted users at once.
   */
  async postUsersUndeleteForPostUsersUndelete(
    body: Types.IdentityUsersBulkRestoreUsersInput,
  ): Promise<Result<Types.IdentityUsersBulkRestoreUsersOutput, ApiError>> {
    const url = '/v1/users:undelete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkRestoreUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersBulkRestoreUsersOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk unsuspend user accounts
   *
   * Unsuspends multiple user accounts at once.
   */
  async postUsersUnsuspendForPostUsersUnsuspend(
    body: Types.IdentityUsersBulkUnsuspendUsersInput,
  ): Promise<Result<Types.IdentityUsersBulkUnsuspendUsersOutput, ApiError>> {
    const url = '/v1/users:unsuspend';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkUnsuspendUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersBulkUnsuspendUsersOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk partial update users
   *
   * Updates multiple users with partial data.
   */
  async postUsersUpdate(body: Types.IdentityUsersBulkUpdateUsersInput): Promise<Result<void, ApiError>> {
    const url = '/v1/users:update';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersBulkUpdateUsersInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get user by ID
   *
   * Retrieves detailed information for a specific user by their unique identifier.
   */
  async getUsersForGetUsersByUserId(userId: string): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update user by ID
   *
   * Fully updates a user by ID with complete user data.
   */
  async putUsers(userId: string, body: Types.IdentityUsersCreateUserInput): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersCreateUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Soft delete user by ID
   *
   * Soft deletes a user by ID (can be restored). Users can delete their own account.
   */
  async deleteUsers(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update user by ID
   *
   * Updates specific fields of a user by ID.
   */
  async patchUsers(userId: string, body: Types.IdentityUsersUpdateUserInput): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityUsersUpdateUserInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Check if user exists by ID
   *
   * Checks if a user exists by ID without returning the body.
   */
  async headUsers(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Activate user account
   *
   * Activates a user account by ID.
   */
  async postUsersActivateForPostUsersByUserIdActivate(userId: string): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deactivate user account
   *
   * Deactivates a user account by ID.
   */
  async postUsersDeactivateForPostUsersByUserIdDeactivate(userId: string): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Hard delete user by ID (irreversible purge)
   *
   * Permanently deletes a user by ID (irreversible).
   */
  async postUsersPurgeForPostUsersByUserIdPurge(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/users/${userId}:purge`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Suspend user account
   *
   * Suspends a user account by ID.
   */
  async postUsersSuspendForPostUsersByUserIdSuspend(userId: string): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}:suspend`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Undelete soft-deleted user by ID
   *
   * Restores a soft-deleted user by ID.
   */
  async postUsersUndeleteForPostUsersByUserIdUndelete(userId: string): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}:undelete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Unsuspend user account
   *
   * Unsuspends a user account by ID.
   */
  async postUsersUnsuspendForPostUsersByUserIdUnsuspend(userId: string): Promise<Result<Types.IdentityUsersUserDto, ApiError>> {
    const url = `/v1/users/${userId}:unsuspend`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityUsersUserDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createUsersModule(client: ApiClient): UsersModule {
  return new UsersModule(client);
}
