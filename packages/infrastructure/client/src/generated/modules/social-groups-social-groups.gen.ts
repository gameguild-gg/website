/**
 * @game-guild/client - SocialGroupsSocialGroups Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialGroupsSocialGroupsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiSocialGroupsForGetApiSocialGroups(query?: {
    tenantId?: string;
    ownerId?: string;
    type?: Types.SocialGroupsSocialGroupType;
    visibility?: Types.SocialGroupsSocialGroupVisibility;
    status?: Types.SocialGroupsSocialGroupStatus;
    search?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.SocialGroupsSocialGroupDto>, ApiError>> {
    const url = '/api/social/groups';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.SocialGroupsSocialGroupDto>, ApiError>;
  }

  /**
   */
  async postApiSocialGroups(body: Types.SocialGroupsCreateSocialGroupInput): Promise<Result<Types.SocialGroupsSocialGroupDto, ApiError>> {
    const url = '/api/social/groups';

    // Validate request body
    const validatedBody = safeParse(Types.SocialGroupsCreateSocialGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialGroupsSocialGroupDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialGroupsForGetApiSocialGroupsById(id: string): Promise<Result<Types.SocialGroupsSocialGroupDto, ApiError>> {
    const url = `/api/social/groups/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialGroupsSocialGroupDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiSocialGroups(id: string, body: Types.SocialGroupsUpdateSocialGroupInput): Promise<Result<Types.SocialGroupsSocialGroupDto, ApiError>> {
    const url = `/api/social/groups/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialGroupsUpdateSocialGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialGroupsSocialGroupDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiSocialGroupsActivate(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialGroupsArchive(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/archive`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiSocialGroupsMembers(
    id: string,
    query?: { status?: Types.SocialGroupsSocialGroupMembershipStatus; skip?: number; take?: number },
  ): Promise<Result<Array<Types.SocialGroupsSocialGroupMemberDto>, ApiError>> {
    const url = `/api/social/groups/${id}/members`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.SocialGroupsSocialGroupMemberDto>, ApiError>;
  }

  /**
   */
  async postApiSocialGroupsMembers(
    id: string,
    body: Types.SocialGroupsJoinSocialGroupInput,
  ): Promise<Result<Types.SocialGroupsSocialGroupMemberDto, ApiError>> {
    const url = `/api/social/groups/${id}/members`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialGroupsJoinSocialGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialGroupsSocialGroupMemberDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiSocialGroupsMembers(id: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/members/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialGroupsMembersApprove(id: string, userId: string, body: Types.SocialGroupsApproveSocialGroupMemberInput): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/members/${userId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialGroupsApproveSocialGroupMemberInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialGroupsMembersReject(id: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/members/${userId}/reject`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async putApiSocialGroupsMembersRole(id: string, userId: string, body: Types.SocialGroupsChangeSocialGroupMemberRoleInput): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/members/${userId}/role`;

    // Validate request body
    const validatedBody = safeParse(Types.SocialGroupsChangeSocialGroupMemberRoleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiSocialGroupsSuspend(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/social/groups/${id}/suspend`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createSocialGroupsSocialGroupsModule(client: ApiClient): SocialGroupsSocialGroupsModule {
  return new SocialGroupsSocialGroupsModule(client);
}
