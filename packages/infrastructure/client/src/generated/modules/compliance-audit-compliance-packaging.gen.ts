/**
 * @game-guild/client - ComplianceAuditCompliancePackaging Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditCompliancePackagingModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiAuditCompliancePackagingForGetApiAuditCompliancePackaging(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditCompliancePackageSummary[], ApiError>> {
    const url = '/api/audit/compliance-packaging';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditCompliancePackageSummary[], ApiError>;
  }

  /**
   */
  async postApiAuditCompliancePackaging(
    body: Types.ComplianceAuditCreateCompliancePackageInput,
  ): Promise<Result<Types.ComplianceAuditCompliancePackageOutput, ApiError>> {
    const url = '/api/audit/compliance-packaging';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateCompliancePackageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCompliancePackageOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditCompliancePackagingForGetApiAuditCompliancePackagingById(
    id: string,
  ): Promise<Result<Types.ComplianceAuditCompliancePackageOutput, ApiError>> {
    const url = `/api/audit/compliance-packaging/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCompliancePackageOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditCompliancePackagingDownload(id: string): Promise<Result<Blob, ApiError>> {
    const url = `/api/audit/compliance-packaging/${id}/download`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      responseType: 'blob',
      headers: { Accept: 'application/zip' },
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   */
  async getApiAuditCompliancePackagingVerification(id: string): Promise<Result<Types.ComplianceAuditComplianceArtifactVerification, ApiError>> {
    const url = `/api/audit/compliance-packaging/${id}/verification`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceArtifactVerificationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditCompliancePackagingDocumentsForGetApiAuditCompliancePackagingDocuments(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput[], ApiError>> {
    const url = '/api/audit/compliance-packaging/documents';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditComplianceDocumentOutput[], ApiError>;
  }

  /**
   */
  async postApiAuditCompliancePackagingDocuments(
    body: Types.ComplianceAuditUploadComplianceDocumentInput,
  ): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput, ApiError>> {
    const url = '/api/audit/compliance-packaging/documents';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUploadComplianceDocumentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceDocumentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditCompliancePackagingDocumentsForGetApiAuditCompliancePackagingDocumentsById(
    id: string,
  ): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput, ApiError>> {
    const url = `/api/audit/compliance-packaging/documents/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceDocumentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiAuditCompliancePackagingDocumentsReview(
    id: string,
    body: Types.ComplianceAuditReviewComplianceDocumentInput,
  ): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput, ApiError>> {
    const url = `/api/audit/compliance-packaging/documents/${id}/review`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReviewComplianceDocumentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceDocumentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAuditCompliancePackagingTemplates(): Promise<Result<Types.ComplianceAuditComplianceFrameworkTemplate[], ApiError>> {
    const url = '/api/audit/compliance-packaging/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditComplianceFrameworkTemplate[], ApiError>;
  }

  /**
   */
  async getAuditCompliancePackagingForGetAuditCompliancePackaging(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditCompliancePackageSummary[], ApiError>> {
    const url = '/v1/audit/compliance-packaging';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditCompliancePackageSummary[], ApiError>;
  }

  /**
   */
  async postAuditCompliancePackaging(
    body: Types.ComplianceAuditCreateCompliancePackageInput,
  ): Promise<Result<Types.ComplianceAuditCompliancePackageOutput, ApiError>> {
    const url = '/v1/audit/compliance-packaging';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateCompliancePackageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCompliancePackageOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditCompliancePackagingForGetAuditCompliancePackagingById(id: string): Promise<Result<Types.ComplianceAuditCompliancePackageOutput, ApiError>> {
    const url = `/v1/audit/compliance-packaging/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCompliancePackageOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditCompliancePackagingDownload(id: string): Promise<Result<Blob, ApiError>> {
    const url = `/v1/audit/compliance-packaging/${id}/download`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      responseType: 'blob',
      headers: { Accept: 'application/zip' },
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   */
  async getAuditCompliancePackagingVerification(id: string): Promise<Result<Types.ComplianceAuditComplianceArtifactVerification, ApiError>> {
    const url = `/v1/audit/compliance-packaging/${id}/verification`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceArtifactVerificationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditCompliancePackagingDocumentsForGetAuditCompliancePackagingDocuments(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput[], ApiError>> {
    const url = '/v1/audit/compliance-packaging/documents';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditComplianceDocumentOutput[], ApiError>;
  }

  /**
   */
  async postAuditCompliancePackagingDocuments(
    body: Types.ComplianceAuditUploadComplianceDocumentInput,
  ): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput, ApiError>> {
    const url = '/v1/audit/compliance-packaging/documents';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUploadComplianceDocumentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceDocumentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditCompliancePackagingDocumentsForGetAuditCompliancePackagingDocumentsById(
    id: string,
  ): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput, ApiError>> {
    const url = `/v1/audit/compliance-packaging/documents/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceDocumentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAuditCompliancePackagingDocumentsReview(
    id: string,
    body: Types.ComplianceAuditReviewComplianceDocumentInput,
  ): Promise<Result<Types.ComplianceAuditComplianceDocumentOutput, ApiError>> {
    const url = `/v1/audit/compliance-packaging/documents/${id}/review`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReviewComplianceDocumentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditComplianceDocumentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAuditCompliancePackagingTemplates(): Promise<Result<Types.ComplianceAuditComplianceFrameworkTemplate[], ApiError>> {
    const url = '/v1/audit/compliance-packaging/templates';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditComplianceFrameworkTemplate[], ApiError>;
  }
}

export function createComplianceAuditCompliancePackagingModule(client: ApiClient): ComplianceAuditCompliancePackagingModule {
  return new ComplianceAuditCompliancePackagingModule(client);
}
