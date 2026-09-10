/**
 * @game-guild/client - ApiAdminEvents Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ApiAdminEventsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postAdminEventsReplay(eventId: string, query?: { consumerName?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/admin/events/${eventId}:replay`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getAdminEventsDeadLetters(): Promise<Result<Array<Types.APIEventingDeadLetterEvent>, ApiError>> {
    const url = '/v1/admin/events/dead-letters';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.APIEventingDeadLetterEvent>, ApiError>;
  }

  /**
   */
  async getAdminEventsStatus(): Promise<Result<Types.APIEventingEventTransportStatus, ApiError>> {
    const url = '/v1/admin/events/status';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIEventingEventTransportStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createApiAdminEventsModule(client: ApiClient): ApiAdminEventsModule {
  return new ApiAdminEventsModule(client);
}
