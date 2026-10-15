/**
 * @game-guild/client - ComplianceAuditSecurity Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditSecurityModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get unified security audit logs from all sources with filtering and pagination.
   */
  async getAdminSecurityAudit(query?: {
    SourceType?: Types.ComplianceAuditSecurityAuditSourceType;
    UserId?: string;
    TenantId?: string;
    ActionType?: string;
    Success?: boolean;
    RiskLevel?: Types.ComplianceAuditAuditRiskLevel;
    StartDate?: string;
    EndDate?: string;
    IpAddress?: string;
    SearchText?: string;
    Skip?: number;
    Take?: number;
    SortBy?: string;
    SortDirection?: string;
  }): Promise<Result<Types.ComplianceAuditUnifiedSecurityAuditOutput, ApiError>> {
    const url = '/v1/admin/security-audit';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditUnifiedSecurityAuditOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Export unified security audit logs to CSV.
   */
  async postAdminSecurityAuditExport(body: Types.ComplianceAuditUnifiedSecurityAuditInput): Promise<Result<Blob, ApiError>> {
    const url = '/v1/admin/security-audit/:export';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditUnifiedSecurityAuditInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   * Get authentication attempt logs specifically.
   *
   * Deliberately kept on the SystemAdmin policy: the authentication-attempt surface
   * has no tenant-scoped filter today (the request and the underlying query are
   * platform-wide), so opening it to the Audit.Read self-service tier would leak
   * cross-tenant authentication data. Revisit together with a tenant filter on
   * GameGuild.Compliance.Audit.AuthenticationAuditRequest.
   */
  async getAdminSecurityAuditAuthentication(query?: {
    UserId?: string;
    Email?: string;
    IpAddress?: string;
    Success?: boolean;
    FailureReason?: string;
    StartDate?: string;
    EndDate?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditAuthenticationAuditOutput, ApiError>> {
    const url = '/v1/admin/security-audit/authentication';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuthenticationAuditOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get security audit dashboard with aggregated statistics.
   */
  async getAdminSecurityAuditDashboard(query?: {
    startDate?: string;
    endDate?: string;
    tenantId?: string;
  }): Promise<Result<Types.ComplianceAuditSecurityAuditDashboard, ApiError>> {
    const url = '/v1/admin/security-audit/dashboard';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditSecurityAuditDashboardSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get permission audit logs specifically.
   */
  async getAdminSecurityAuditPermissions(query?: {
    UserId?: string;
    TenantId?: string;
    PermissionType?: string;
    OperationType?: string;
    ResourceType?: string;
    Success?: boolean;
    StartDate?: string;
    EndDate?: string;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.ComplianceAuditPermissionAuditOutput, ApiError>> {
    const url = '/v1/admin/security-audit/permissions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditPermissionAuditOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditSecurityModule(client: ApiClient): ComplianceAuditSecurityModule {
  return new ComplianceAuditSecurityModule(client);
}
