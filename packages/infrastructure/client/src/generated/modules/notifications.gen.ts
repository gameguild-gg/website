/**
 * @game-guild/client - Notifications Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class NotificationsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiNotificationsForGetApiNotifications(query?: {
    skip?: number;
    take?: number;
    isRead?: boolean;
  }): Promise<Result<Array<Types.NotificationsControllersNotificationDto>, ApiError>> {
    const url = '/api/notifications';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.NotificationsControllersNotificationDto>, ApiError>;
  }

  /**
   */
  async getApiNotificationsForGetApiNotificationsById(id: string): Promise<Result<Types.NotificationsControllersNotificationDto, ApiError>> {
    const url = `/api/notifications/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersNotificationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiNotifications(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/notifications/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiNotificationsRead(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/notifications/${id}/read`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiNotificationsUnread(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/notifications/${id}/unread`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiNotificationsPreferences(): Promise<Result<Types.NotificationsControllersNotificationPreferenceDto, ApiError>> {
    const url = '/api/notifications/preferences';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersNotificationPreferenceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiNotificationsPreferences(
    body: Types.NotificationsControllersUpdatePreferencesInput,
  ): Promise<Result<Types.NotificationsControllersNotificationPreferenceDto, ApiError>> {
    const url = '/api/notifications/preferences';

    // Validate request body
    const validatedBody = safeParse(Types.NotificationsControllersUpdatePreferencesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersNotificationPreferenceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiNotificationsPreferencesDigestFrequency(
    body: Types.NotificationsControllersUpdateDigestFrequencyInput,
  ): Promise<Result<Types.NotificationsControllersDigestFrequencyOutput, ApiError>> {
    const url = '/api/notifications/preferences/digest-frequency';

    // Validate request body
    const validatedBody = safeParse(Types.NotificationsControllersUpdateDigestFrequencyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersDigestFrequencyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiNotificationsPreferencesMutedTypes(
    body: Types.NotificationsControllersUpdateMutedTypesInput,
  ): Promise<Result<Types.NotificationsControllersMutedTypesOutput, ApiError>> {
    const url = '/api/notifications/preferences/muted-types';

    // Validate request body
    const validatedBody = safeParse(Types.NotificationsControllersUpdateMutedTypesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersMutedTypesOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiNotificationsPreferencesQuietHours(body: Types.NotificationsControllersSetQuietHoursInput): Promise<Result<void, ApiError>> {
    const url = '/api/notifications/preferences/quiet-hours';

    // Validate request body
    const validatedBody = safeParse(Types.NotificationsControllersSetQuietHoursInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async deleteApiNotificationsRead(): Promise<Result<Types.NotificationsControllersDeletedCountOutput, ApiError>> {
    const url = '/api/notifications/read';

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersDeletedCountOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postApiNotificationsReadAll(): Promise<Result<void, ApiError>> {
    const url = '/api/notifications/read-all';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiNotificationsTypesCatalog(): Promise<Result<Array<Types.NotificationsControllersNotificationTypeCatalogEntry>, ApiError>> {
    const url = '/api/notifications/types-catalog';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.NotificationsControllersNotificationTypeCatalogEntry>, ApiError>;
  }

  /**
   */
  async getApiNotificationsUnreadCount(): Promise<Result<Types.NotificationsControllersUnreadCountOutput, ApiError>> {
    const url = '/api/notifications/unread-count';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersUnreadCountOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getEmailDeliveryDeadletters(query?: {
    skip?: number;
    take?: number;
    type?: string;
    email?: string;
  }): Promise<Result<Types.PagedResultDeadLetterDto, ApiError>> {
    const url = '/api/v1/email-delivery/deadletters';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultDeadLetterDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getEmailDeliveryEmailEvents(query?: {
    skip?: number;
    take?: number;
    eventType?: string;
    email?: string;
    providerMessageId?: string;
  }): Promise<Result<Types.PagedResultEmailDeliveryEventDto, ApiError>> {
    const url = '/api/v1/email-delivery/email-events';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultEmailDeliveryEventDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postEmailDeliveryNotificationsRequeue(id: string): Promise<Result<Types.NotificationsControllersRequeueOutput, ApiError>> {
    const url = `/api/v1/email-delivery/notifications/${id}:requeue`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersRequeueOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getEmailDeliveryNotificationsTimeline(id: string): Promise<Result<Types.NotificationsControllersNotificationTimelineDto, ApiError>> {
    const url = `/api/v1/email-delivery/notifications/${id}/timeline`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersNotificationTimelineDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getEmailDeliverySuppressions(query?: {
    skip?: number;
    take?: number;
    includeReleased?: boolean;
  }): Promise<Result<Types.PagedResultEmailSuppressionDto, ApiError>> {
    const url = '/api/v1/email-delivery/suppressions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultEmailSuppressionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteEmailDeliverySuppressions(email: string): Promise<Result<Types.NotificationsControllersUnsuppressOutput, ApiError>> {
    const url = `/api/v1/email-delivery/suppressions/${email}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersUnsuppressOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * SES email delivery events webhook (public, SNS signature-verified)
   */
  async postNotificationsEmailEvents(): Promise<Result<void, ApiError>> {
    const url = '/api/v1/notifications/email-events';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * One-click unsubscribe (public, signed token)
   */
  async getNotificationsUnsubscribe(query?: { token?: string }): Promise<Result<Types.NotificationsControllersUnsubscribeOutput, ApiError>> {
    const url = '/api/v1/notifications/unsubscribe';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.NotificationsControllersUnsubscribeOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createNotificationsModule(client: ApiClient): NotificationsModule {
  return new NotificationsModule(client);
}
