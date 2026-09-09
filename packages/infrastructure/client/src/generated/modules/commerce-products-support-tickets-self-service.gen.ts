/**
 * @game-guild/client - CommerceProductsSupportTicketsSelfService Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsSupportTicketsSelfServiceModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getSupportTicketsMine(query?: {
    status?: Types.CommerceProductsSupportTicketStatus;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultSupportTicketDto, ApiError>> {
    const url = '/v1/support/tickets/mine';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultSupportTicketDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postSupportTicketsMine(body: Types.CommerceProductsCreateMySupportTicketInput): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = '/v1/support/tickets/mine';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreateMySupportTicketInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsSupportTicketDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postSupportTicketsMineMessages(
    ticketId: string,
    body: Types.CommerceProductsAddMySupportTicketMessageInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/support/tickets/mine/${ticketId}/messages`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsAddMySupportTicketMessageInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsSupportTicketDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceProductsSupportTicketsSelfServiceModule(client: ApiClient): CommerceProductsSupportTicketsSelfServiceModule {
  return new CommerceProductsSupportTicketsSelfServiceModule(client);
}
