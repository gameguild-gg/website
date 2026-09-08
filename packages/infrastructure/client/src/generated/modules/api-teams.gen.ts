/**
 * @game-guild/client - ApiTeams Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ApiTeamsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTeamsForGetTeams(query?: {
    search?: string;
    visibility?: Types.TeamsTeamVisibility;
    status?: Types.TeamsTeamStatus;
    includeArchived?: boolean;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.APITeamsTeamDto>, ApiError>> {
    const url = '/v1/teams';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APITeamsTeamDto>, ApiError>;
  }

  /**
   */
  async postTeams(body: Types.APITeamsCreateTeamInput): Promise<Result<Types.APITeamsTeamDto, ApiError>> {
    const url = '/v1/teams';

    // Validate request body
    const validatedBody = safeParse(Types.APITeamsCreateTeamInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTeamsForGetTeamsByTeamId(teamId: string): Promise<Result<Types.APITeamsTeamDto, ApiError>> {
    const url = `/v1/teams/${teamId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putTeams(teamId: string, body: Types.APITeamsUpdateTeamInput): Promise<Result<Types.APITeamsTeamDto, ApiError>> {
    const url = `/v1/teams/${teamId}`;

    // Validate request body
    const validatedBody = safeParse(Types.APITeamsUpdateTeamInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTeams(teamId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/teams/${teamId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postTeamsRestore(teamId: string): Promise<Result<Types.APITeamsTeamDto, ApiError>> {
    const url = `/v1/teams/${teamId}:restore`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTeamsInvitations(teamId: string): Promise<Result<Array<Types.APITeamsTeamInvitationDto>, ApiError>> {
    const url = `/v1/teams/${teamId}/invitations`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APITeamsTeamInvitationDto>, ApiError>;
  }

  /**
   */
  async postTeamsInvitations(teamId: string, body: Types.APITeamsCreateTeamInvitationInput): Promise<Result<Types.APITeamsTeamInvitationCreatedDto, ApiError>> {
    const url = `/v1/teams/${teamId}/invitations`;

    // Validate request body
    const validatedBody = safeParse(Types.APITeamsCreateTeamInvitationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamInvitationCreatedDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTeamsInvitations(teamId: string, invitationId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/teams/${teamId}/invitations/${invitationId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postTeamsMembers(teamId: string, body: Types.APITeamsAddTeamMemberInput): Promise<Result<Types.APITeamsTeamMemberDto, ApiError>> {
    const url = `/v1/teams/${teamId}/members`;

    // Validate request body
    const validatedBody = safeParse(Types.APITeamsAddTeamMemberInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamMemberDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putTeamsMembers(teamId: string, userId: string, body: Types.APITeamsChangeTeamMemberInput): Promise<Result<Types.APITeamsTeamMemberDto, ApiError>> {
    const url = `/v1/teams/${teamId}/members/${userId}`;

    // Validate request body
    const validatedBody = safeParse(Types.APITeamsChangeTeamMemberInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamMemberDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTeamsMembers(teamId: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/teams/${teamId}/members/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAccept(invitationId: string): Promise<Result<Types.APITeamsTeamDto, ApiError>> {
    const url = `/v1/teams/invitations/${invitationId}:accept`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTeamsInvitationsAcceptForPostTeamsInvitationsAccept(
    body: Types.APITeamsAcceptTeamInvitationInput,
  ): Promise<Result<Types.APITeamsTeamDto, ApiError>> {
    const url = '/v1/teams/invitations/accept';

    // Validate request body
    const validatedBody = safeParse(Types.APITeamsAcceptTeamInvitationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APITeamsTeamDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTeamsMine(query?: {
    includeArchived?: boolean;
    search?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Array<Types.APITeamsTeamDto>, ApiError>> {
    const url = '/v1/teams/mine';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APITeamsTeamDto>, ApiError>;
  }

  /**
   */
  async getTeamsMyInvitations(): Promise<Result<Array<Types.APITeamsMyTeamInvitationDto>, ApiError>> {
    const url = '/v1/teams/my-invitations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APITeamsMyTeamInvitationDto>, ApiError>;
  }
}

export function createApiTeamsModule(client: ApiClient): ApiTeamsModule {
  return new ApiTeamsModule(client);
}
