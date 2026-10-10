/**
 * @game-guild/client - CommerceBillingEvents Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceBillingEventsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List billing webhook inbox events
   *
   * Pages through the durable billing webhook inbox: provider events accepted from Stripe, PayPal, Apple App Store and Google Pay, with their processing status, attempt counters and error details. Supports filtering by status (processed/failed/pending), provider, provider event type and acceptance date range. Payload bodies are never returned.
   */
  async getBillingEventsForGetBillingEvents(query?: {
    status?: string;
    provider?: string;
    eventType?: string;
    fromUtc?: string;
    toUtc?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultBillingWebhookEventListItemDto, ApiError>> {
    const url = '/api/v1/billing/events';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultBillingWebhookEventListItemDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a billing webhook inbox event by id
   *
   * Retrieves one durable webhook inbox event by its local identifier, including processing status, attempt count, error message and tenant/subscription references.
   */
  async getBillingEventsForGetBillingEventsByEventId(eventId: string): Promise<Result<Types.CommerceBillingBillingWebhookEventDto, ApiError>> {
    const url = `/api/v1/billing/events/${eventId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingBillingWebhookEventDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List durable billing integration events from the platform outbox
   *
   * Pages through the named billing integration events (webhook processed/failed, invoice paid, subscription renewed/cancelled) recorded in the platform durable outbox, with their delivery status. Dead-lettered events can be replayed through the platform admin event transport endpoints.
   */
  async getBillingEventsOutboxForGetBillingEventsOutbox(query?: {
    eventName?: string;
    status?: string;
    fromUtc?: string;
    toUtc?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultBillingOutboxEventDto, ApiError>> {
    const url = '/api/v1/billing/events/outbox';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultBillingOutboxEventDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get one durable billing integration event by id
   *
   * Retrieves a single named billing integration event from the platform outbox read model by its durable event identifier.
   */
  async getBillingEventsOutboxForGetBillingEventsOutboxByEventId(eventId: string): Promise<Result<Types.CommerceBillingBillingOutboxEventDto, ApiError>> {
    const url = `/api/v1/billing/events/outbox/${eventId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingBillingOutboxEventDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceBillingEventsModule(client: ApiClient): CommerceBillingEventsModule {
  return new CommerceBillingEventsModule(client);
}
