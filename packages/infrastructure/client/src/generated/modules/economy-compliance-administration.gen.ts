/**
 * @game-guild/client - EconomyComplianceAdministration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyComplianceAdministrationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAdminEconomyComplianceFinancialCrimeCasesForGetAdminEconomyComplianceFinancialCrimeCases(query?: {
    state?: Types.ComplianceFinancialCrimeFinancialCrimeCaseState;
    take?: number;
  }): Promise<Result<Types.ComplianceFinancialCrimeFinancialCrimeCase[], ApiError>> {
    const url = '/api/v1/admin/economy/compliance/financial-crime/cases';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceFinancialCrimeFinancialCrimeCase[], ApiError>;
  }

  /**
   */
  async getAdminEconomyComplianceFinancialCrimeCasesForGetAdminEconomyComplianceFinancialCrimeCasesByCaseId(
    caseId: string,
  ): Promise<Result<Types.ComplianceFinancialCrimeFinancialCrimeCaseDetails, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/financial-crime/cases/${caseId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceFinancialCrimeFinancialCrimeCaseDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyComplianceFinancialCrimeCasesAssignment(
    caseId: string,
    body: Types.APIControllersAssignFinancialCrimeCaseInput,
  ): Promise<Result<Types.ComplianceFinancialCrimeFinancialCrimeCase, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/financial-crime/cases/${caseId}/assignment`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersAssignFinancialCrimeCaseInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceFinancialCrimeFinancialCrimeCaseSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyComplianceFinancialCrimeCasesDecisions(
    caseId: string,
    body: Types.APIControllersDecideFinancialCrimeCaseInput,
  ): Promise<Result<Types.ComplianceFinancialCrimeFinancialCrimeCaseDecision, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/financial-crime/cases/${caseId}/decisions`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersDecideFinancialCrimeCaseInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceFinancialCrimeFinancialCrimeCaseDecisionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyComplianceFinancialCrimeCasesRegulatoryReferences(
    caseId: string,
    body: Types.APIControllersRecordRegulatoryReferenceInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/financial-crime/cases/${caseId}/regulatory-references`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersRecordRegulatoryReferenceInputSchema, body, 'request');

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
  async getAdminEconomyComplianceTrustSafetyAppeals(query?: {
    state?: Types.TrustSafetyTrustSafetyAppealState;
    take?: number;
  }): Promise<Result<Types.TrustSafetyTrustSafetyAppeal[], ApiError>> {
    const url = '/api/v1/admin/economy/compliance/trust-safety/appeals';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.TrustSafetyTrustSafetyAppeal[], ApiError>;
  }

  /**
   */
  async postAdminEconomyComplianceTrustSafetyAppealsAssignment(
    appealId: string,
    body: Types.APIControllersAssignTrustSafetyAppealInput,
  ): Promise<Result<Types.TrustSafetyTrustSafetyAppeal, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/trust-safety/appeals/${appealId}/assignment`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersAssignTrustSafetyAppealInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TrustSafetyTrustSafetyAppealSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyComplianceTrustSafetyAppealsDecisions(
    appealId: string,
    body: Types.APIControllersDecideTrustSafetyAppealInput,
  ): Promise<Result<Types.TrustSafetyTrustSafetyAppeal, ApiError>> {
    const url = `/api/v1/admin/economy/compliance/trust-safety/appeals/${appealId}/decisions`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersDecideTrustSafetyAppealInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TrustSafetyTrustSafetyAppealSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyComplianceAdministrationModule(client: ApiClient): EconomyComplianceAdministrationModule {
  return new EconomyComplianceAdministrationModule(client);
}
