/**
 * @game-guild/client - ComplianceAuditReplay Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ComplianceAuditReplayModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Readable capture alias (issue #169): the same bounded, durably audited
   *             selection as the POST capture, addressed by query parameters so an investigator can
   *             link and re-fetch one frozen range. The response is never cacheable.
   */
  async getApiAuditReplaySnapshots(query?: { start?: string; end?: string }): Promise<Result<Types.ComplianceAuditAuditReplaySnapshotOutput, ApiError>> {
    const url = '/api/audit/replay/snapshots';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditReplaySnapshotOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Captures up to10000events and16MiB of safe canonical JSON; oversized selections fail atomically.
   *
   * Null tenant context selects global-only rows. SHA256 detects content changes against
   *             an independently protected expected digest; it does not certify event provenance or synchronize source clocks.
   */
  async postApiAuditReplaySnapshots(
    body: Types.ComplianceAuditCaptureAuditReplayInput,
  ): Promise<Result<Types.ComplianceAuditAuditReplaySnapshotOutput, ApiError>> {
    const url = '/api/audit/replay/snapshots';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCaptureAuditReplayInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditReplaySnapshotOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reconstructs the chronological incident timeline of one client-held snapshot
   *             (issue #169). The document is revalidated against every capture invariant and its
   *             canonical serialization must match the expected digest; otherwise nothing is returned.
   *
   * The reconstruction adds per-user journeys and per-session summaries. A digest
   *             match detects content changes; it does not establish provenance, actor attribution or
   *             source clock synchronization.
   */
  async postApiAuditReplaySnapshotsReconstruct(
    body: Types.ComplianceAuditReconstructAuditReplayInput,
  ): Promise<Result<Types.ComplianceAuditAuditReplayReconstructionOutput, ApiError>> {
    const url = '/api/audit/replay/snapshots:reconstruct';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReconstructAuditReplayInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditReplayReconstructionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Readable capture alias (issue #169): the same bounded, durably audited
   *             selection as the POST capture, addressed by query parameters so an investigator can
   *             link and re-fetch one frozen range. The response is never cacheable.
   */
  async getAdminAuditLogsReplaySnapshots(query?: { start?: string; end?: string }): Promise<Result<Types.ComplianceAuditAuditReplaySnapshotOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/replay/snapshots';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditReplaySnapshotOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Captures up to10000events and16MiB of safe canonical JSON; oversized selections fail atomically.
   *
   * Null tenant context selects global-only rows. SHA256 detects content changes against
   *             an independently protected expected digest; it does not certify event provenance or synchronize source clocks.
   */
  async postAdminAuditLogsReplaySnapshots(
    body: Types.ComplianceAuditCaptureAuditReplayInput,
  ): Promise<Result<Types.ComplianceAuditAuditReplaySnapshotOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/replay/snapshots';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditCaptureAuditReplayInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditReplaySnapshotOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reconstructs the chronological incident timeline of one client-held snapshot
   *             (issue #169). The document is revalidated against every capture invariant and its
   *             canonical serialization must match the expected digest; otherwise nothing is returned.
   *
   * The reconstruction adds per-user journeys and per-session summaries. A digest
   *             match detects content changes; it does not establish provenance, actor attribution or
   *             source clock synchronization.
   */
  async postAdminAuditLogsReplaySnapshotsReconstruct(
    body: Types.ComplianceAuditReconstructAuditReplayInput,
  ): Promise<Result<Types.ComplianceAuditAuditReplayReconstructionOutput, ApiError>> {
    const url = '/v1/admin/audit-logs/replay/snapshots:reconstruct';

    // Validate request body
    const validatedBody = safeParse(Types.ComplianceAuditReconstructAuditReplayInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ComplianceAuditAuditReplayReconstructionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createComplianceAuditReplayModule(client: ApiClient): ComplianceAuditReplayModule {
  return new ComplianceAuditReplayModule(client);
}
