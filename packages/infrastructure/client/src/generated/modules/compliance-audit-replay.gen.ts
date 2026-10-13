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
}

export function createComplianceAuditReplayModule(client: ApiClient): ComplianceAuditReplayModule {
  return new ComplianceAuditReplayModule(client);
}
