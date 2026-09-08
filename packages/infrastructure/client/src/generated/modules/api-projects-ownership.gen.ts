/**
 * @game-guild/client - ApiProjectsOwnership Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ApiProjectsOwnershipModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getProjectsOwnership(projectId: string): Promise<Result<Types.APIProjectsProjectOwnershipDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectOwnershipDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipAgreements(
    projectId: string,
    body: Types.APIProjectsCreateProjectTeamAgreementInput,
  ): Promise<Result<Types.APIProjectsProjectTeamAgreementDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/agreements`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsCreateProjectTeamAgreementInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamAgreementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipAgreementsAccept(projectId: string, agreementId: string): Promise<Result<Types.APIProjectsProjectTeamAgreementDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/agreements/${agreementId}/accept`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamAgreementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipAgreementsCancel(projectId: string, agreementId: string): Promise<Result<Types.APIProjectsProjectTeamAgreementDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/agreements/${agreementId}/cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamAgreementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipAgreementsComplete(projectId: string, agreementId: string): Promise<Result<Types.APIProjectsProjectTeamAgreementDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/agreements/${agreementId}/complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamAgreementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipAgreementsCounter(
    projectId: string,
    agreementId: string,
    body: Types.APIProjectsCounterProjectTeamAgreementInput,
  ): Promise<Result<Types.APIProjectsProjectTeamAgreementDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/agreements/${agreementId}/counter`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsCounterProjectTeamAgreementInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamAgreementDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipAllocations(
    projectId: string,
    body: Types.APIProjectsCreateProjectAllocationInput,
  ): Promise<Result<Types.APIProjectsProjectAllocationDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/allocations`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsCreateProjectAllocationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectAllocationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putProjectsOwnershipAllocations(
    projectId: string,
    allocationId: string,
    body: Types.APIProjectsUpdateProjectAllocationInput,
  ): Promise<Result<Types.APIProjectsProjectAllocationDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/allocations/${allocationId}`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsUpdateProjectAllocationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectAllocationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteProjectsOwnershipAllocations(projectId: string, allocationId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/allocations/${allocationId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postProjectsOwnershipOwnerTeam(
    projectId: string,
    body: Types.APIProjectsTransferProjectOwnerTeamInput,
  ): Promise<Result<Types.APIProjectsProjectOwnershipDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/owner-team`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsTransferProjectOwnerTeamInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectOwnershipDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProjectsOwnershipTeams(
    projectId: string,
    body: Types.APIProjectsAddProjectTeamInput,
  ): Promise<Result<Types.APIProjectsProjectTeamOwnershipDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/teams`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsAddProjectTeamInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamOwnershipDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putProjectsOwnershipTeams(
    projectId: string,
    projectTeamId: string,
    body: Types.APIProjectsUpdateProjectTeamInput,
  ): Promise<Result<Types.APIProjectsProjectTeamOwnershipDto, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/teams/${projectTeamId}`;

    // Validate request body
    const validatedBody = safeParse(Types.APIProjectsUpdateProjectTeamInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIProjectsProjectTeamOwnershipDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteProjectsOwnershipTeams(projectId: string, projectTeamId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/projects/${projectId}/ownership/teams/${projectTeamId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createApiProjectsOwnershipModule(client: ApiClient): ApiProjectsOwnershipModule {
  return new ApiProjectsOwnershipModule(client);
}
