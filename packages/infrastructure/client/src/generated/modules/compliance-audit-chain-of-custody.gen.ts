/**
 * @game-guild/client - ComplianceAuditChainOfCustody Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditChainOfCustodyModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists registered custodians with their authorization scopes.
   */
  async getApiAuditChainOfCustodyCustodians(): Promise<Result<Types.ComplianceAuditEvidenceCustodianOutput[], ApiError>> {
    const url = '/api/audit/chain-of-custody/custodians';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditEvidenceCustodianOutput[], ApiError>;
  }

  /**
   * Registers a custodian with authorization scopes. Fail-closed: only registered active custodians may act on evidence.
   */
  async postApiAuditChainOfCustodyCustodians(
    body: Types.ComplianceAuditRegisterCustodianInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceCustodianOutput, ApiError>> {
    const url = '/api/audit/chain-of-custody/custodians';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRegisterCustodianInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceCustodianOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revokes a custodian's authorization (revocation is effective immediately and logged).
   */
  async postApiAuditChainOfCustodyCustodiansRevoke(
    custodianId: string,
    body: Types.ComplianceAuditRevokeCustodianInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceCustodianOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/custodians/${custodianId}:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRevokeCustodianInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceCustodianOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists preserved evidence for the tenant (custody-gated access; every read is logged).
   */
  async getApiAuditChainOfCustodyEvidenceForGetApiAuditChainOfCustodyEvidence(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput[], ApiError>> {
    const url = '/api/audit/chain-of-custody/evidence';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditCustodyEvidenceOutput[], ApiError>;
  }

  /**
   * Preserves evidence under chain of custody with content hashing (audit logs, security events, value snapshots, sealed packages, or raw content).
   */
  async postApiAuditChainOfCustodyEvidence(
    body: Types.ComplianceAuditPreserveEvidenceInput,
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = '/api/audit/chain-of-custody/evidence';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditPreserveEvidenceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one evidence item with its full custody timeline (access logged; broken seals require forensic scope).
   */
  async getApiAuditChainOfCustodyEvidenceForGetApiAuditChainOfCustodyEvidenceByEvidenceId(
    evidenceId: string,
    query?: { purpose?: string },
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/evidence/${evidenceId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deliberately breaks the evidence seal for forensic inspection (forensic scope required; custody event recorded).
   */
  async postApiAuditChainOfCustodyEvidenceBreakSeal(
    evidenceId: string,
    body: Types.ComplianceAuditBreakSealInput,
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/evidence/${evidenceId}:break-seal`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditBreakSealInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Releases evidence from custody (release scope required; blocked while a legal hold is active).
   */
  async postApiAuditChainOfCustodyEvidenceRelease(
    evidenceId: string,
    body: Types.ComplianceAuditReleaseEvidenceInput,
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/evidence/${evidenceId}:release`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReleaseEvidenceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Verifies the preserved content against its recorded SHA-256 hash.
   */
  async getApiAuditChainOfCustodyEvidenceIntegrity(evidenceId: string): Promise<Result<Types.ComplianceAuditEvidenceIntegrityOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/evidence/${evidenceId}/integrity`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceIntegrityOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists legal holds with their preserved evidence and notification status.
   */
  async getApiAuditChainOfCustodyLegalHoldsForGetApiAuditChainOfCustodyLegalHolds(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditLegalHoldOutput[], ApiError>> {
    const url = '/api/audit/chain-of-custody/legal-holds';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditLegalHoldOutput[], ApiError>;
  }

  /**
   * Creates a legal hold: immediately preserves every in-scope target under custody and records stakeholder notifications.
   */
  async postApiAuditChainOfCustodyLegalHolds(body: Types.ComplianceAuditCreateLegalHoldInput): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = '/api/audit/chain-of-custody/legal-holds';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateLegalHoldInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one legal hold with preserved evidence ids and notifications.
   */
  async getApiAuditChainOfCustodyLegalHoldsForGetApiAuditChainOfCustodyLegalHoldsByHoldId(
    holdId: string,
  ): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/legal-holds/${holdId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Dispatches pending hold notifications (automated stakeholder notification workflow).
   */
  async postApiAuditChainOfCustodyLegalHoldsNotify(holdId: string): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/legal-holds/${holdId}:notify`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Releases a legal hold (preserved evidence and custody chains are retained as investigation records).
   */
  async postApiAuditChainOfCustodyLegalHoldsRelease(
    holdId: string,
    body: Types.ComplianceAuditReleaseHoldInput,
  ): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/legal-holds/${holdId}:release`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReleaseHoldInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Builds a court-admissible custody package: full documentation, verification summary, and signed expert certification.
   */
  async postApiAuditChainOfCustodyPackages(
    body: Types.ComplianceAuditBuildCustodyPackageInput,
  ): Promise<Result<Types.ComplianceAuditCourtAdmissiblePackageOutput, ApiError>> {
    const url = '/api/audit/chain-of-custody/packages';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditBuildCustodyPackageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCourtAdmissiblePackageOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists evidence transfers with proof-verification status.
   */
  async getApiAuditChainOfCustodyTransfers(query?: {
    evidenceId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditEvidenceTransferOutput[], ApiError>> {
    const url = '/api/audit/chain-of-custody/transfers';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditEvidenceTransferOutput[], ApiError>;
  }

  /**
   * Initiates a custody transfer: challenge nonce + ECDSA-signed proof binding evidence hash and both custodians.
   */
  async postApiAuditChainOfCustodyTransfers(
    body: Types.ComplianceAuditInitiateTransferInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceTransferOutput, ApiError>> {
    const url = '/api/audit/chain-of-custody/transfers';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditInitiateTransferInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceTransferOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Acknowledges a transfer as the receiving custodian: verifies the proof, then counter-signs and takes custody.
   */
  async postApiAuditChainOfCustodyTransfersAcknowledge(
    transferId: string,
    body: Types.ComplianceAuditAcknowledgeTransferInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceTransferOutput, ApiError>> {
    const url = `/api/audit/chain-of-custody/transfers/${transferId}:acknowledge`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAcknowledgeTransferInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceTransferOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists registered custodians with their authorization scopes.
   */
  async getAuditChainOfCustodyCustodians(): Promise<Result<Types.ComplianceAuditEvidenceCustodianOutput[], ApiError>> {
    const url = '/v1/audit/chain-of-custody/custodians';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditEvidenceCustodianOutput[], ApiError>;
  }

  /**
   * Registers a custodian with authorization scopes. Fail-closed: only registered active custodians may act on evidence.
   */
  async postAuditChainOfCustodyCustodians(
    body: Types.ComplianceAuditRegisterCustodianInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceCustodianOutput, ApiError>> {
    const url = '/v1/audit/chain-of-custody/custodians';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRegisterCustodianInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceCustodianOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revokes a custodian's authorization (revocation is effective immediately and logged).
   */
  async postAuditChainOfCustodyCustodiansRevoke(
    custodianId: string,
    body: Types.ComplianceAuditRevokeCustodianInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceCustodianOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/custodians/${custodianId}:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditRevokeCustodianInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceCustodianOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists preserved evidence for the tenant (custody-gated access; every read is logged).
   */
  async getAuditChainOfCustodyEvidenceForGetAuditChainOfCustodyEvidence(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput[], ApiError>> {
    const url = '/v1/audit/chain-of-custody/evidence';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditCustodyEvidenceOutput[], ApiError>;
  }

  /**
   * Preserves evidence under chain of custody with content hashing (audit logs, security events, value snapshots, sealed packages, or raw content).
   */
  async postAuditChainOfCustodyEvidence(
    body: Types.ComplianceAuditPreserveEvidenceInput,
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = '/v1/audit/chain-of-custody/evidence';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditPreserveEvidenceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one evidence item with its full custody timeline (access logged; broken seals require forensic scope).
   */
  async getAuditChainOfCustodyEvidenceForGetAuditChainOfCustodyEvidenceByEvidenceId(
    evidenceId: string,
    query?: { purpose?: string },
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/evidence/${evidenceId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deliberately breaks the evidence seal for forensic inspection (forensic scope required; custody event recorded).
   */
  async postAuditChainOfCustodyEvidenceBreakSeal(
    evidenceId: string,
    body: Types.ComplianceAuditBreakSealInput,
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/evidence/${evidenceId}:break-seal`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditBreakSealInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Releases evidence from custody (release scope required; blocked while a legal hold is active).
   */
  async postAuditChainOfCustodyEvidenceRelease(
    evidenceId: string,
    body: Types.ComplianceAuditReleaseEvidenceInput,
  ): Promise<Result<Types.ComplianceAuditCustodyEvidenceOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/evidence/${evidenceId}:release`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReleaseEvidenceInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCustodyEvidenceOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Verifies the preserved content against its recorded SHA-256 hash.
   */
  async getAuditChainOfCustodyEvidenceIntegrity(evidenceId: string): Promise<Result<Types.ComplianceAuditEvidenceIntegrityOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/evidence/${evidenceId}/integrity`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceIntegrityOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists legal holds with their preserved evidence and notification status.
   */
  async getAuditChainOfCustodyLegalHoldsForGetAuditChainOfCustodyLegalHolds(query?: {
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditLegalHoldOutput[], ApiError>> {
    const url = '/v1/audit/chain-of-custody/legal-holds';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditLegalHoldOutput[], ApiError>;
  }

  /**
   * Creates a legal hold: immediately preserves every in-scope target under custody and records stakeholder notifications.
   */
  async postAuditChainOfCustodyLegalHolds(body: Types.ComplianceAuditCreateLegalHoldInput): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = '/v1/audit/chain-of-custody/legal-holds';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCreateLegalHoldInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Returns one legal hold with preserved evidence ids and notifications.
   */
  async getAuditChainOfCustodyLegalHoldsForGetAuditChainOfCustodyLegalHoldsByHoldId(
    holdId: string,
  ): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/legal-holds/${holdId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Dispatches pending hold notifications (automated stakeholder notification workflow).
   */
  async postAuditChainOfCustodyLegalHoldsNotify(holdId: string): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/legal-holds/${holdId}:notify`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Releases a legal hold (preserved evidence and custody chains are retained as investigation records).
   */
  async postAuditChainOfCustodyLegalHoldsRelease(
    holdId: string,
    body: Types.ComplianceAuditReleaseHoldInput,
  ): Promise<Result<Types.ComplianceAuditLegalHoldOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/legal-holds/${holdId}:release`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReleaseHoldInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditLegalHoldOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Builds a court-admissible custody package: full documentation, verification summary, and signed expert certification.
   */
  async postAuditChainOfCustodyPackages(
    body: Types.ComplianceAuditBuildCustodyPackageInput,
  ): Promise<Result<Types.ComplianceAuditCourtAdmissiblePackageOutput, ApiError>> {
    const url = '/v1/audit/chain-of-custody/packages';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditBuildCustodyPackageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditCourtAdmissiblePackageOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists evidence transfers with proof-verification status.
   */
  async getAuditChainOfCustodyTransfers(query?: {
    evidenceId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.ComplianceAuditEvidenceTransferOutput[], ApiError>> {
    const url = '/v1/audit/chain-of-custody/transfers';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ComplianceAuditEvidenceTransferOutput[], ApiError>;
  }

  /**
   * Initiates a custody transfer: challenge nonce + ECDSA-signed proof binding evidence hash and both custodians.
   */
  async postAuditChainOfCustodyTransfers(
    body: Types.ComplianceAuditInitiateTransferInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceTransferOutput, ApiError>> {
    const url = '/v1/audit/chain-of-custody/transfers';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditInitiateTransferInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceTransferOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Acknowledges a transfer as the receiving custodian: verifies the proof, then counter-signs and takes custody.
   */
  async postAuditChainOfCustodyTransfersAcknowledge(
    transferId: string,
    body: Types.ComplianceAuditAcknowledgeTransferInput,
  ): Promise<Result<Types.ComplianceAuditEvidenceTransferOutput, ApiError>> {
    const url = `/v1/audit/chain-of-custody/transfers/${transferId}:acknowledge`;

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditAcknowledgeTransferInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditEvidenceTransferOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditChainOfCustodyModule(client: ApiClient): ComplianceAuditChainOfCustodyModule {
  return new ComplianceAuditChainOfCustodyModule(client);
}
