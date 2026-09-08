/**
 * @game-guild/client - CommerceProductsSupportTickets Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsSupportTicketsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getSupportTickets(query?: {
    tenantId?: string;
    status?: Types.CommerceProductsSupportTicketStatus;
    priority?: Types.CommerceProductsSupportTicketPriority;
    search?: string;
    skip?: number;
    take?: number;
    customerId?: string;
  }): Promise<Result<Types.PagedResultSupportTicketDto, ApiError>> {
    const url = '/v1/support/tickets';

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
  async postSupportTickets(body: Types.CommerceProductsCreateSupportTicketInput): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = '/v1/support/tickets';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreateSupportTicketInputSchema, body, 'request');

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
  async getSupportTicketById(ticketId: string, query?: { tenantId?: string }): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/support/tickets/${ticketId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
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
  async postSupportTicketsAssign(
    ticketId: string,
    body: Types.CommerceProductsAssignSupportTicketInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/support/tickets/${ticketId}:assign`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsAssignSupportTicketInputSchema, body, 'request');

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
  async postSupportTicketsClose(
    ticketId: string,
    body: Types.CommerceProductsCloseSupportTicketInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/support/tickets/${ticketId}:close`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCloseSupportTicketInputSchema, body, 'request');

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
  async postSupportTicketsResolve(
    ticketId: string,
    body: Types.CommerceProductsResolveSupportTicketInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/support/tickets/${ticketId}:resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsResolveSupportTicketInputSchema, body, 'request');

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
  async postSupportTicketsMessages(
    ticketId: string,
    body: Types.CommerceProductsAddSupportTicketMessageInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/support/tickets/${ticketId}/messages`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsAddSupportTicketMessageInputSchema, body, 'request');

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

export function createCommerceProductsSupportTicketsModule(client: ApiClient): CommerceProductsSupportTicketsModule {
  return new CommerceProductsSupportTicketsModule(client);
}
