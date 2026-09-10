/**
 * @game-guild/client - EconomyAdministration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyAdministrationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAdminEconomyAdRewardsPendingClaims(query?: {
    confirmed?: boolean;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageAdRewardPendingClaimOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/pending-claims';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageAdRewardPendingClaimOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsReconciliations(query?: {
    network?: string;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageAdRewardReconciliationOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/reconciliations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.FinanceEconomyOperationsEconomyOperationalPageAdRewardReconciliationOperationalStatusSchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsReports(query?: {
    network?: string;
    limit?: number;
  }): Promise<Result<Array<Types.FinanceEconomyAdRewardsDurableAdProviderReportStatus>, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/reports';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyAdRewardsDurableAdProviderReportStatus>, ApiError>;
  }

  /**
   */
  async postAdminEconomyAdRewardsReports(
    body: Types.FinanceEconomyAdRewardsAdProviderReport,
  ): Promise<Result<Types.FinanceEconomyAdRewardsDurableAdProviderReportImportResult, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/reports';

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyAdRewardsAdProviderReportSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyAdRewardsDurableAdProviderReportImportResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsSessionsForGetAdminEconomyAdRewardsSessions(query?: {
    state?: Types.FinanceEconomyAdRewardsDurableAdRewardSessionState;
    network?: string;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageAdRewardSessionOperationalSummary, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/sessions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageAdRewardSessionOperationalSummarySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsSessionsForGetAdminEconomyAdRewardsSessionsBySessionId(
    sessionId: string,
  ): Promise<Result<Types.FinanceEconomyAdRewardsAdRewardSessionOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ad-rewards/sessions/${sessionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyAdRewardsAdRewardSessionOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyBountiesExpired(): Promise<Result<Array<Types.FinanceEconomyBountiesDurableBountyView>, ApiError>> {
    const url = '/api/v1/admin/economy/bounties/expired';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyBountiesDurableBountyView>, ApiError>;
  }

  /**
   */
  async getAdminEconomyCapabilitiesConfiguration(query?: {
    includeInactiveKillSwitches?: boolean;
    limit?: number;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyCapabilityConfigurationSnapshot, ApiError>> {
    const url = '/api/v1/admin/economy/capabilities/configuration';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyCapabilityConfigurationSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyCapabilitiesReadiness(
    body: Types.APIControllersInspectEconomyCapabilityReadinessInput,
  ): Promise<Result<Types.FinanceEconomyRiskEconomyCapabilityEvaluationResult, ApiError>> {
    const url = '/api/v1/admin/economy/capabilities/readiness';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersInspectEconomyCapabilityReadinessInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyCapabilityEvaluationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyCustodyObservationsForGetAdminEconomyCustodyObservations(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageEconomyCustodyObservationOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/custody/observations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.FinanceEconomyOperationsEconomyOperationalPageEconomyCustodyObservationOperationalStatusSchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyCustodyObservations(
    body: Types.FinanceEconomyReservesCustodyObservationCommand,
  ): Promise<Result<Types.FinanceEconomyReservesDurableCustodyObservation, ApiError>> {
    const url = '/api/v1/admin/economy/custody/observations';

    // Validate request body
    const validatedBody = safeParse(Types.FinanceEconomyReservesCustodyObservationCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyReservesDurableCustodyObservationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyCustodyObservationsForGetAdminEconomyCustodyObservationsByObservationId(
    observationId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsEconomyCustodyObservationOperationalStatus, ApiError>> {
    const url = `/api/v1/admin/economy/custody/observations/${observationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyCustodyObservationOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitches(
    body: Types.APIControllersActivateEconomyKillSwitchInput,
  ): Promise<Result<Types.FinanceEconomyRiskEconomyKillSwitchState, ApiError>> {
    const url = '/api/v1/admin/economy/kill-switches';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersActivateEconomyKillSwitchInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitchesRelease(killSwitchId: string): Promise<Result<Types.FinanceEconomyRiskEconomyKillSwitchState, ApiError>> {
    const url = `/api/v1/admin/economy/kill-switches/${killSwitchId}/release`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitchesReleaseApprovals(
    killSwitchId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.FinanceEconomyRiskEconomyKillSwitchState, ApiError>> {
    const url = `/api/v1/admin/economy/kill-switches/${killSwitchId}/release-approvals`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersEconomyStepUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitchesReleaseProposals(
    killSwitchId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.FinanceEconomyRiskEconomyKillSwitchState, ApiError>> {
    const url = `/api/v1/admin/economy/kill-switches/${killSwitchId}/release-proposals`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersEconomyStepUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerAnchorsForGetAdminEconomyLedgerAnchors(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageEconomyAnchorOperationalDetails, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/anchors';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageEconomyAnchorOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerAnchors(
    body: Types.APIControllersPublishEconomyAnchorInput,
  ): Promise<Result<Types.FinanceEconomyLedgerEconomyAnchorPublicationResult, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/anchors';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersPublishEconomyAnchorInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyLedgerEconomyAnchorPublicationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerAnchorsForGetAdminEconomyLedgerAnchorsByAnchorId(
    anchorId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsEconomyAnchorOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/anchors/${anchorId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyAnchorOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerAnchorsVerifications(
    anchorId: string,
  ): Promise<Result<Array<Types.FinanceEconomyOperationsEconomyAnchorVerificationOperationalStatus>, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/anchors/${anchorId}/verifications`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyOperationsEconomyAnchorVerificationOperationalStatus>, ApiError>;
  }

  /**
   */
  async postAdminEconomyLedgerAnchorsVerificationRuns(): Promise<Result<Types.FinanceEconomyLedgerAnchorVerificationRunResult, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/anchors/verification-runs';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyLedgerAnchorVerificationRunResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerHealth(): Promise<Result<Types.FinanceEconomyOperationsEconomyLedgerHealthSnapshot, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/health';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyLedgerHealthSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerProjectionGenerationsForGetAdminEconomyLedgerProjectionGenerations(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageEconomyProjectionGenerationOperationalDetails, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/projection-generations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.FinanceEconomyOperationsEconomyOperationalPageEconomyProjectionGenerationOperationalDetailsSchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerProjectionGenerations(): Promise<Result<Types.FinanceEconomyProjectionsProjectionGenerationState, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/projection-generations';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyProjectionsProjectionGenerationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerProjectionGenerationsForGetAdminEconomyLedgerProjectionGenerationsByGeneration(
    generation: number,
  ): Promise<Result<Types.FinanceEconomyOperationsEconomyProjectionGenerationOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/projection-generations/${generation}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyProjectionGenerationOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerProjectionGenerationsApprovals(
    generation: number,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.FinanceEconomyProjectionsProjectionGenerationState, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/projection-generations/${generation}/approvals`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersEconomyStepUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyProjectionsProjectionGenerationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerProjectionGenerationsAudit(
    generation: number,
  ): Promise<Result<Array<Types.FinanceEconomyOperationsEconomyProjectionApprovalAuditEntry>, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/projection-generations/${generation}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyOperationsEconomyProjectionApprovalAuditEntry>, ApiError>;
  }

  /**
   */
  async getAdminEconomyLedgerVerificationRunsForGetAdminEconomyLedgerVerificationRuns(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageEconomyJournalVerificationRunDetails, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/verification-runs';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageEconomyJournalVerificationRunDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerVerificationRuns(): Promise<Result<Types.FinanceEconomyLedgerJournalIntegrityRunResult, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/verification-runs';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyLedgerJournalIntegrityRunResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerVerificationRunsForGetAdminEconomyLedgerVerificationRunsByVerificationId(
    verificationId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsEconomyJournalVerificationRunDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/verification-runs/${verificationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyJournalVerificationRunDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceOutbox(query?: {
    published?: boolean;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageMarketplaceOutboxOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/marketplace/outbox';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageMarketplaceOutboxOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceRefundsForGetAdminEconomyMarketplaceRefunds(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageMarketplaceRefundOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/marketplace/refunds';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyOperationalPageMarketplaceRefundOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceRefundsForGetAdminEconomyMarketplaceRefundsByRefundId(
    refundId: string,
  ): Promise<Result<Types.FinanceEconomyMarketplaceMarketplaceRefundOperationalStatus, ApiError>> {
    const url = `/api/v1/admin/economy/marketplace/refunds/${refundId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyMarketplaceMarketplaceRefundOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceSettlementsForGetAdminEconomyMarketplaceSettlements(query?: {
    status?: Types.FinanceEconomyMarketplaceMarketplaceSettlementStatus;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageMarketplaceSettlementOperationalSummary, ApiError>> {
    const url = '/api/v1/admin/economy/marketplace/settlements';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.FinanceEconomyOperationsEconomyOperationalPageMarketplaceSettlementOperationalSummarySchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceSettlementsForGetAdminEconomyMarketplaceSettlementsBySettlementId(
    settlementId: string,
  ): Promise<Result<Types.FinanceEconomyMarketplaceMarketplaceSettlementOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/marketplace/settlements/${settlementId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyMarketplaceMarketplaceSettlementOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyMarketplaceSettlementsRefund(
    settlementId: string,
    body: Types.APIControllersRefundMarketplaceSettlementInput,
  ): Promise<Result<Types.FinanceEconomyMarketplaceDurableMarketplaceRefundResult, ApiError>> {
    const url = `/api/v1/admin/economy/marketplace/settlements/${settlementId}:refund`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersRefundMarketplaceSettlementInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyMarketplaceDurableMarketplaceRefundResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reserve FIFO funds for a fully approved payout request
   *
   * Tenant and actor authority come exclusively from the authenticated actor context. Fresh MFA and the full capability control plane are required.
   */
  async postAdminEconomyPayoutRequestsReserve(
    requestId: string,
    body: Types.APIControllersReserveApprovedPayoutExecutionInput,
  ): Promise<Result<Types.APIControllersEconomyPayoutExecutionOperationDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/${requestId}/reserve`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersReserveApprovedPayoutExecutionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersEconomyPayoutExecutionOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List tenant-scoped payout execution operations
   */
  async getAdminEconomyPayoutRequestsOperationsForGetAdminEconomyPayoutRequestsOperations(query?: {
    take?: number;
  }): Promise<Result<Array<Types.APIControllersEconomyPayoutExecutionOperationDto>, ApiError>> {
    const url = '/api/v1/admin/economy/payout-requests/operations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APIControllersEconomyPayoutExecutionOperationDto>, ApiError>;
  }

  /**
   * Get a tenant-scoped payout execution operation
   */
  async getAdminEconomyPayoutRequestsOperationsForGetAdminEconomyPayoutRequestsOperationsByOperationId(
    operationId: string,
  ): Promise<Result<Types.APIControllersEconomyPayoutExecutionOperationDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/operations/${operationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersEconomyPayoutExecutionOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Atomically authorize and enqueue an approved payout dispatch
   */
  async postAdminEconomyPayoutRequestsOperationsDispatch(
    operationId: string,
    body: Types.APIControllersDispatchPayoutExecutionInput,
  ): Promise<Result<Types.APIControllersEconomyPayoutExecutionOperationDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/operations/${operationId}/dispatch`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersDispatchPayoutExecutionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersEconomyPayoutExecutionOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reconcile an in-flight payout directly with its provider
   */
  async postAdminEconomyPayoutRequestsOperationsReconcile(
    operationId: string,
  ): Promise<Result<Types.APIControllersEconomyPayoutExecutionOperationDto, ApiError>> {
    const url = `/api/v1/admin/economy/payout-requests/operations/${operationId}/reconcile`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersEconomyPayoutExecutionOperationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyPoliciesForGetAdminEconomyPolicies(query?: {
    capability?: Types.FinanceEconomyRiskEconomyValueMovementCapability;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageEconomyCapabilityPolicyOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/policies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.FinanceEconomyOperationsEconomyOperationalPageEconomyCapabilityPolicyOperationalStatusSchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyPolicies(
    body: Types.APIControllersProposeEconomyPolicyInput,
  ): Promise<Result<Types.FinanceEconomyRiskEconomyCapabilityPolicy, ApiError>> {
    const url = '/api/v1/admin/economy/policies';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersProposeEconomyPolicyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyCapabilityPolicySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyPoliciesForGetAdminEconomyPoliciesByPolicyId(
    policyId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsEconomyPolicyOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/policies/${policyId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyPolicyOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyPoliciesApprove(
    policyId: string,
    body: Types.APIControllersApproveEconomyPolicyInput,
  ): Promise<Result<Types.FinanceEconomyRiskEconomyCapabilityPolicy, ApiError>> {
    const url = `/api/v1/admin/economy/policies/${policyId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersApproveEconomyPolicyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskEconomyCapabilityPolicySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyPoliciesAudit(policyId: string): Promise<Result<Array<Types.FinanceEconomyOperationsEconomyPolicyAuditEntry>, ApiError>> {
    const url = `/api/v1/admin/economy/policies/${policyId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyOperationsEconomyPolicyAuditEntry>, ApiError>;
  }

  /**
   */
  async getAdminEconomyReservesActive(): Promise<Result<Types.FinanceEconomyOperationsEconomyActiveReserveOperationalDetails, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/active';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyActiveReserveOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyReservesLiabilities(): Promise<Result<Types.FinanceEconomyReservesEconomyLiabilitySnapshot, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/liabilities';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyReservesEconomyLiabilitySnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyReservesProposalsForGetAdminEconomyReservesProposals(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyOperationsEconomyOperationalPageEconomyReserveProposalOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/proposals';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.FinanceEconomyOperationsEconomyOperationalPageEconomyReserveProposalOperationalStatusSchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyReservesProposals(
    body: Types.APIControllersProposeEconomyReserveInput,
  ): Promise<Result<Types.FinanceEconomyReservesDurableReserveProposalState, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/proposals';

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersProposeEconomyReserveInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyReservesDurableReserveProposalStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyReservesProposalsForGetAdminEconomyReservesProposalsByProposalId(
    proposalId: string,
  ): Promise<Result<Types.FinanceEconomyOperationsEconomyReserveProposalOperationalStatus, ApiError>> {
    const url = `/api/v1/admin/economy/reserves/proposals/${proposalId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyOperationsEconomyReserveProposalOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyReservesProposalsApprove(
    proposalId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.FinanceEconomyReservesReserveHead, ApiError>> {
    const url = `/api/v1/admin/economy/reserves/proposals/${proposalId}/approve`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersEconomyStepUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyReservesReserveHeadSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyAdministrationModule(client: ApiClient): EconomyAdministrationModule {
  return new EconomyAdministrationModule(client);
}
