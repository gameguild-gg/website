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
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageAdRewardPendingClaimOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/pending-claims';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageAdRewardPendingClaimOperationalStatusSchema, result.data, 'response');
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
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageAdRewardReconciliationOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/reconciliations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageAdRewardReconciliationOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsReports(query?: {
    network?: string;
    limit?: number;
  }): Promise<Result<Array<Types.EconomyAdRewardsDurableAdProviderReportStatus>, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/reports';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyAdRewardsDurableAdProviderReportStatus>, ApiError>;
  }

  /**
   */
  async postAdminEconomyAdRewardsReports(
    body: Types.EconomyAdRewardsAdProviderReport,
  ): Promise<Result<Types.EconomyAdRewardsDurableAdProviderReportImportResult, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/reports';

    // Validate request body
    const validatedBody = safeParse(Types.EconomyAdRewardsAdProviderReportSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyAdRewardsDurableAdProviderReportImportResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsSessionsForGetAdminEconomyAdRewardsSessions(query?: {
    state?: Types.EconomyAdRewardsDurableAdRewardSessionState;
    network?: string;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageAdRewardSessionOperationalSummary, ApiError>> {
    const url = '/api/v1/admin/economy/ad-rewards/sessions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageAdRewardSessionOperationalSummarySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyAdRewardsSessionsForGetAdminEconomyAdRewardsSessionsBySessionId(
    sessionId: string,
  ): Promise<Result<Types.EconomyAdRewardsAdRewardSessionOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ad-rewards/sessions/${sessionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyAdRewardsAdRewardSessionOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyBountiesExpired(): Promise<Result<Array<Types.EconomyBountiesDurableBountyView>, ApiError>> {
    const url = '/api/v1/admin/economy/bounties/expired';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyBountiesDurableBountyView>, ApiError>;
  }

  /**
   */
  async getAdminEconomyCapabilitiesConfiguration(query?: {
    includeInactiveKillSwitches?: boolean;
    limit?: number;
  }): Promise<Result<Types.EconomyOperationsEconomyCapabilityConfigurationSnapshot, ApiError>> {
    const url = '/api/v1/admin/economy/capabilities/configuration';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyCapabilityConfigurationSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyCapabilitiesReadiness(
    body: Types.APIControllersInspectEconomyCapabilityReadinessInput,
  ): Promise<Result<Types.EconomyRiskEconomyCapabilityEvaluationResult, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyRiskEconomyCapabilityEvaluationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyCustodyObservationsForGetAdminEconomyCustodyObservations(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageEconomyCustodyObservationOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/custody/observations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageEconomyCustodyObservationOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyCustodyObservations(
    body: Types.EconomyReservesCustodyObservationCommand,
  ): Promise<Result<Types.EconomyReservesDurableCustodyObservation, ApiError>> {
    const url = '/api/v1/admin/economy/custody/observations';

    // Validate request body
    const validatedBody = safeParse(Types.EconomyReservesCustodyObservationCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyReservesDurableCustodyObservationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyCustodyObservationsForGetAdminEconomyCustodyObservationsByObservationId(
    observationId: string,
  ): Promise<Result<Types.EconomyOperationsEconomyCustodyObservationOperationalStatus, ApiError>> {
    const url = `/api/v1/admin/economy/custody/observations/${observationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyCustodyObservationOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitches(
    body: Types.APIControllersActivateEconomyKillSwitchInput,
  ): Promise<Result<Types.EconomyRiskEconomyKillSwitchState, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitchesRelease(killSwitchId: string): Promise<Result<Types.EconomyRiskEconomyKillSwitchState, ApiError>> {
    const url = `/api/v1/admin/economy/kill-switches/${killSwitchId}/release`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitchesReleaseApprovals(
    killSwitchId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.EconomyRiskEconomyKillSwitchState, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyKillSwitchesReleaseProposals(
    killSwitchId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.EconomyRiskEconomyKillSwitchState, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyRiskEconomyKillSwitchStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerAnchorsForGetAdminEconomyLedgerAnchors(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageEconomyAnchorOperationalDetails, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/anchors';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageEconomyAnchorOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerAnchors(
    body: Types.APIControllersPublishEconomyAnchorInput,
  ): Promise<Result<Types.EconomyLedgerEconomyAnchorPublicationResult, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyLedgerEconomyAnchorPublicationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerAnchorsForGetAdminEconomyLedgerAnchorsByAnchorId(
    anchorId: string,
  ): Promise<Result<Types.EconomyOperationsEconomyAnchorOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/anchors/${anchorId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyAnchorOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerAnchorsVerifications(
    anchorId: string,
  ): Promise<Result<Array<Types.EconomyOperationsEconomyAnchorVerificationOperationalStatus>, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/anchors/${anchorId}/verifications`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyOperationsEconomyAnchorVerificationOperationalStatus>, ApiError>;
  }

  /**
   */
  async postAdminEconomyLedgerAnchorsVerificationRuns(): Promise<Result<Types.EconomyLedgerAnchorVerificationRunResult, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/anchors/verification-runs';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyLedgerAnchorVerificationRunResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerHealth(): Promise<Result<Types.EconomyOperationsEconomyLedgerHealthSnapshot, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/health';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyLedgerHealthSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerProjectionGenerationsForGetAdminEconomyLedgerProjectionGenerations(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageEconomyProjectionGenerationOperationalDetails, ApiError>> {
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
        Types.EconomyOperationsEconomyOperationalPageEconomyProjectionGenerationOperationalDetailsSchema,
        result.data,
        'response',
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerProjectionGenerations(): Promise<Result<Types.EconomyProjectionsProjectionGenerationState, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/projection-generations';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyProjectionsProjectionGenerationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerProjectionGenerationsForGetAdminEconomyLedgerProjectionGenerationsByGeneration(
    generation: number,
  ): Promise<Result<Types.EconomyOperationsEconomyProjectionGenerationOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/projection-generations/${generation}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyProjectionGenerationOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerProjectionGenerationsApprovals(
    generation: number,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.EconomyProjectionsProjectionGenerationState, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyProjectionsProjectionGenerationStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerProjectionGenerationsAudit(
    generation: number,
  ): Promise<Result<Array<Types.EconomyOperationsEconomyProjectionApprovalAuditEntry>, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/projection-generations/${generation}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyOperationsEconomyProjectionApprovalAuditEntry>, ApiError>;
  }

  /**
   */
  async getAdminEconomyLedgerVerificationRunsForGetAdminEconomyLedgerVerificationRuns(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageEconomyJournalVerificationRunDetails, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/verification-runs';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageEconomyJournalVerificationRunDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyLedgerVerificationRuns(): Promise<Result<Types.EconomyLedgerJournalIntegrityRunResult, ApiError>> {
    const url = '/api/v1/admin/economy/ledger/verification-runs';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyLedgerJournalIntegrityRunResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyLedgerVerificationRunsForGetAdminEconomyLedgerVerificationRunsByVerificationId(
    verificationId: string,
  ): Promise<Result<Types.EconomyOperationsEconomyJournalVerificationRunDetails, ApiError>> {
    const url = `/api/v1/admin/economy/ledger/verification-runs/${verificationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyJournalVerificationRunDetailsSchema, result.data, 'response');
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
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageMarketplaceOutboxOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/marketplace/outbox';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageMarketplaceOutboxOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceRefundsForGetAdminEconomyMarketplaceRefunds(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageMarketplaceRefundOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/marketplace/refunds';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageMarketplaceRefundOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceRefundsForGetAdminEconomyMarketplaceRefundsByRefundId(
    refundId: string,
  ): Promise<Result<Types.EconomyMarketplaceMarketplaceRefundOperationalStatus, ApiError>> {
    const url = `/api/v1/admin/economy/marketplace/refunds/${refundId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyMarketplaceMarketplaceRefundOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceSettlementsForGetAdminEconomyMarketplaceSettlements(query?: {
    status?: Types.EconomyMarketplaceMarketplaceSettlementStatus;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageMarketplaceSettlementOperationalSummary, ApiError>> {
    const url = '/api/v1/admin/economy/marketplace/settlements';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageMarketplaceSettlementOperationalSummarySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyMarketplaceSettlementsForGetAdminEconomyMarketplaceSettlementsBySettlementId(
    settlementId: string,
  ): Promise<Result<Types.EconomyMarketplaceMarketplaceSettlementOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/marketplace/settlements/${settlementId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyMarketplaceMarketplaceSettlementOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyMarketplaceSettlementsRefund(
    settlementId: string,
    body: Types.APIControllersRefundMarketplaceSettlementInput,
  ): Promise<Result<Types.EconomyMarketplaceDurableMarketplaceRefundResult, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyMarketplaceDurableMarketplaceRefundResultSchema, result.data, 'response');
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
    capability?: Types.EconomyRiskEconomyValueMovementCapability;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageEconomyCapabilityPolicyOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/policies';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageEconomyCapabilityPolicyOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyPolicies(body: Types.APIControllersProposeEconomyPolicyInput): Promise<Result<Types.EconomyRiskEconomyCapabilityPolicy, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyRiskEconomyCapabilityPolicySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyPoliciesForGetAdminEconomyPoliciesByPolicyId(
    policyId: string,
  ): Promise<Result<Types.EconomyOperationsEconomyPolicyOperationalDetails, ApiError>> {
    const url = `/api/v1/admin/economy/policies/${policyId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyPolicyOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyPoliciesApprove(
    policyId: string,
    body: Types.APIControllersApproveEconomyPolicyInput,
  ): Promise<Result<Types.EconomyRiskEconomyCapabilityPolicy, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyRiskEconomyCapabilityPolicySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyPoliciesAudit(policyId: string): Promise<Result<Array<Types.EconomyOperationsEconomyPolicyAuditEntry>, ApiError>> {
    const url = `/api/v1/admin/economy/policies/${policyId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.EconomyOperationsEconomyPolicyAuditEntry>, ApiError>;
  }

  /**
   */
  async getAdminEconomyReservesActive(): Promise<Result<Types.EconomyOperationsEconomyActiveReserveOperationalDetails, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/active';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyActiveReserveOperationalDetailsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyReservesLiabilities(): Promise<Result<Types.EconomyReservesEconomyLiabilitySnapshot, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/liabilities';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyReservesEconomyLiabilitySnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyReservesProposalsForGetAdminEconomyReservesProposals(query?: {
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.EconomyOperationsEconomyOperationalPageEconomyReserveProposalOperationalStatus, ApiError>> {
    const url = '/api/v1/admin/economy/reserves/proposals';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyOperationalPageEconomyReserveProposalOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyReservesProposals(
    body: Types.APIControllersProposeEconomyReserveInput,
  ): Promise<Result<Types.EconomyReservesDurableReserveProposalState, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyReservesDurableReserveProposalStateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyReservesProposalsForGetAdminEconomyReservesProposalsByProposalId(
    proposalId: string,
  ): Promise<Result<Types.EconomyOperationsEconomyReserveProposalOperationalStatus, ApiError>> {
    const url = `/api/v1/admin/economy/reserves/proposals/${proposalId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.EconomyOperationsEconomyReserveProposalOperationalStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyReservesProposalsApprove(
    proposalId: string,
    body: Types.APIControllersEconomyStepUpInput,
  ): Promise<Result<Types.EconomyReservesReserveHead, ApiError>> {
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
      const validatedData = safeParse(Types.EconomyReservesReserveHeadSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createEconomyAdministrationModule(client: ApiClient): EconomyAdministrationModule {
  return new EconomyAdministrationModule(client);
}
