/**
 * @game-guild/client - ComplianceConsent Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceConsentModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postApiComplianceConsentDataSubjectRequests(
    body: Types.ComplianceConsentSubmitDataSubjectRequestCommand,
  ): Promise<Result<Types.ComplianceConsentDataSubjectRequestDto, ApiError>> {
    const url = '/api/compliance/consent/data-subject-requests';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceConsentSubmitDataSubjectRequestCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceConsentDataSubjectRequestDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiComplianceConsentDataSubjectRequestsProcess(
    requestId: string,
    body: Types.ComplianceConsentProcessRequestBody,
  ): Promise<Result<Types.ComplianceConsentDataSubjectRequestDto, ApiError>> {
    const url = `/api/compliance/consent/data-subject-requests/${requestId}/process`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceConsentProcessRequestBodySchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceConsentDataSubjectRequestDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiComplianceConsentDataSubjectRequestsPending(): Promise<Result<Array<Types.ComplianceConsentDataSubjectRequestDto>, ApiError>> {
    const url = '/api/compliance/consent/data-subject-requests/pending';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ComplianceConsentDataSubjectRequestDto>, ApiError>;
  }

  /**
   */
  async postApiComplianceConsentGrant(body: Types.ComplianceConsentGrantConsentCommand): Promise<Result<Types.ComplianceConsentUserConsentDto, ApiError>> {
    const url = '/api/compliance/consent/grant';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceConsentGrantConsentCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceConsentUserConsentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiComplianceConsentPolicies(query?: { tenantId?: string }): Promise<Result<Array<Types.ComplianceConsentConsentPolicyDto>, ApiError>> {
    const url = '/api/compliance/consent/policies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ComplianceConsentConsentPolicyDto>, ApiError>;
  }

  /**
   */
  async postApiComplianceConsentPolicies(body: Types.ComplianceConsentCreateConsentPolicyCommand): Promise<Result<string, ApiError>> {
    const url = '/api/compliance/consent/policies';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceConsentCreateConsentPolicyCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<string, ApiError>;
  }

  /**
   */
  async postApiComplianceConsentPoliciesVersions(
    policyId: string,
    body: Types.ComplianceConsentPublishVersionInput,
  ): Promise<Result<Types.ComplianceConsentPolicyVersionDto, ApiError>> {
    const url = `/api/compliance/consent/policies/${policyId}/versions`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceConsentPublishVersionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceConsentPolicyVersionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiComplianceConsentRevoke(body: Types.ComplianceConsentRevokeConsentCommand): Promise<Result<void, ApiError>> {
    const url = '/api/compliance/consent/revoke';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceConsentRevokeConsentCommandSchema, body, 'request');

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
  async getApiComplianceConsentUsers(userId: string): Promise<Result<Array<Types.ComplianceConsentUserConsentDto>, ApiError>> {
    const url = `/api/compliance/consent/users/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.ComplianceConsentUserConsentDto>, ApiError>;
  }
}

export function createComplianceConsentModule(client: ApiClient): ComplianceConsentModule {
  return new ComplianceConsentModule(client);
}
