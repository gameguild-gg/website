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
   * Gets the current user's notifications
   */
  async getApiNotificationsForGetApiNotifications(query?: {
    skip?: number;
    take?: number;
    isRead?: boolean;
  }): Promise<Result<Types.NotificationsControllersNotificationDto[], ApiError>> {
    const url = '/api/notifications';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.NotificationsControllersNotificationDto[], ApiError>;
  }

  /**
   * Gets a specific notification by ID
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
   * Deletes a notification
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
   * Marks a notification as read
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
   * Marks a notification as unread
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
   * Gets the current user's notification preferences
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
   * Updates the current user's notification preferences
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
   * Sets the current user's email digest frequency (null, Daily, Weekly or BiWeekly)
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
   * Replaces the current user's muted notification types (full replace; empty list clears all mutes)
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
   * Sets quiet hours for the current user
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
   * Deletes all read notifications for the current user
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
   * Marks all notifications as read for the current user
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
   * Gets the catalog of notification types with category and suppressibility classification (drives the preferences UI)
   */
  async getApiNotificationsTypesCatalog(): Promise<Result<Types.NotificationsControllersNotificationTypeCatalogEntry[], ApiError>> {
    const url = '/api/notifications/types-catalog';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.NotificationsControllersNotificationTypeCatalogEntry[], ApiError>;
  }

  /**
   * Gets the unread notification count for the current user
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
   * Gets dead-lettered notifications (newest first), filterable by notification type and recipient email
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
   * Gets the delivery event feed (newest first), filterable by event type, recipient email and provider message id
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
   * Requeues a dead-lettered notification for another delivery attempt
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
   * Gets the delivery timeline of a notification (its provider events, oldest first); empty when the row has no provider correlation id
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
   * Gets suppressions (newest first); active-only unless includeReleased is true
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
   * Releases the active suppression for an address (admin unsuppress). Idempotent: returns 200 when no active suppression exists.
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
   * Receives SNS notifications for SES delivery events (send, delivery, bounce, complaint, open)
   */
  async postNotificationsEmailEvents(): Promise<Result<void, ApiError>> {
    const url = '/api/v1/notifications/email-events';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Processes a one-click unsubscribe: mutes a type, disables a category, or turns off email entirely
   */
  async getNotificationsUnsubscribe(query?: { token?: string }): Promise<Result<Types.NotificationsControllersUnsubscribeOutput, ApiError>> {
    const url = '/api/v1/notifications/unsubscribe';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
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
