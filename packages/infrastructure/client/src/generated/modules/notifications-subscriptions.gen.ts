/**
 * @game-guild/client - NotificationsSubscriptions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class NotificationsSubscriptionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List subscription billing notifications
   *
   * Lists local billing notification records tied to subscriptions.
   */
  async getNotificationsSubscriptions(query?: {
    tenantId?: string;
    subscriptionId?: string;
    channel?: Types.NotificationsNotificationChannel;
    isSent?: boolean;
    page?: number;
    pageSize?: number;
  }): Promise<Result<Types.PagedResultSubscriptionNotificationDto, ApiError>> {
    const url = '/api/v1/notifications/subscriptions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultSubscriptionNotificationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resend subscription billing notification
   *
   * Creates a new local delivery record from an existing subscription billing notification.
   */
  async postNotificationsSubscriptionsResend(
    notificationId: string,
    body: Types.CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput,
  ): Promise<Result<Types.CommerceSubscriptionsSubscriptionNotificationDto, ApiError>> {
    const url = `/api/v1/notifications/subscriptions/${notificationId}:resend`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsSubscriptionNotificationDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createNotificationsSubscriptionsModule(client: ApiClient): NotificationsSubscriptionsModule {
  return new NotificationsSubscriptionsModule(client);
}
