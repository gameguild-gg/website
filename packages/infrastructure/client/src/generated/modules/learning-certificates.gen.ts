/**
 * @game-guild/client - LearningCertificates Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCertificatesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiCertificates(id: string): Promise<Result<Types.LearningCertificatesCertificateDto, ApiError>> {
    const url = `/api/certificates/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCertificatesCertificateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiCertificatesRevoke(id: string, body: Types.LearningCertificatesRevokeCertificateInput): Promise<Result<void, ApiError>> {
    const url = `/api/certificates/${id}/revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCertificatesRevokeCertificateInputSchema, body, 'request');

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
  async getApiCertificatesCourse(courseId: string): Promise<Result<Array<Types.LearningCertificatesCertificateDto>, ApiError>> {
    const url = `/api/certificates/course/${courseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCertificatesCertificateDto>, ApiError>;
  }

  /**
   */
  async getApiCertificatesExpiring(query?: { days?: number }): Promise<Result<Array<Types.LearningCertificatesCertificateDto>, ApiError>> {
    const url = '/api/certificates/expiring';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCertificatesCertificateDto>, ApiError>;
  }

  /**
   */
  async postApiCertificatesIssue(body: Types.LearningCertificatesIssueCertificateInput): Promise<Result<Types.LearningCertificatesCertificateDto, ApiError>> {
    const url = '/api/certificates/issue';

    // Validate request body
    const validatedBody = safeParse(Types.LearningCertificatesIssueCertificateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCertificatesCertificateDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiCertificatesMy(): Promise<Result<Array<Types.LearningCertificatesCertificateDto>, ApiError>> {
    const url = '/api/certificates/my';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCertificatesCertificateDto>, ApiError>;
  }

  /**
   */
  async postApiCertificatesTemplates(
    body: Types.LearningCertificatesCreateCertificateTemplateInput,
  ): Promise<Result<Types.LearningCertificatesCertificateTemplateDetailDto, ApiError>> {
    const url = '/api/certificates/templates';

    // Validate request body
    const validatedBody = safeParse(Types.LearningCertificatesCreateCertificateTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCertificatesCertificateTemplateDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiCertificatesTemplates(templateId: string): Promise<Result<Types.LearningCertificatesCertificateTemplateDetailDto, ApiError>> {
    const url = `/api/certificates/templates/${templateId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCertificatesCertificateTemplateDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiCertificatesTemplates(
    templateId: string,
    body: Types.LearningCertificatesUpdateCertificateTemplateInput,
  ): Promise<Result<Types.LearningCertificatesCertificateTemplateDetailDto, ApiError>> {
    const url = `/api/certificates/templates/${templateId}`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCertificatesUpdateCertificateTemplateInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCertificatesCertificateTemplateDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiCertificatesTemplates(templateId: string): Promise<Result<void, ApiError>> {
    const url = `/api/certificates/templates/${templateId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiCertificatesTemplatesCourse(courseId: string): Promise<Result<Array<Types.LearningCertificatesCertificateTemplateDto>, ApiError>> {
    const url = `/api/certificates/templates/course/${courseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningCertificatesCertificateTemplateDto>, ApiError>;
  }

  /**
   */
  async getApiCertificatesVerify(certificateNumber: string): Promise<Result<Types.LearningCertificatesCertificateVerificationResult, ApiError>> {
    const url = `/api/certificates/verify/${certificateNumber}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningCertificatesCertificateVerificationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLearningCertificatesModule(client: ApiClient): LearningCertificatesModule {
  return new LearningCertificatesModule(client);
}
