/**
 * @game-guild/client - LaunchPadEvents Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LaunchPadEventsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postLaunchPadEvents(body: Types.LaunchPadCreateLaunchPadEventInput): Promise<Result<Types.LaunchPadLaunchPadEventProjection, ApiError>> {
    const url = '/v1/launch-pad/events';

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadCreateLaunchPadEventInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadEventProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadEventsApplications(
    eventId: string,
    body: Types.LaunchPadSubmitLaunchPadApplicationInput,
  ): Promise<Result<Types.LaunchPadLaunchPadApplicationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/${eventId}/applications`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadSubmitLaunchPadApplicationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadApplicationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLaunchPadEventsApplicationsManagement(eventId: string): Promise<Result<Types.LaunchPadLaunchPadApplicationProjection[], ApiError>> {
    const url = `/v1/launch-pad/events/${eventId}/applications/management`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LaunchPadLaunchPadApplicationProjection[], ApiError>;
  }

  /**
   */
  async getLaunchPadEventsRegistrationsManagement(eventId: string): Promise<Result<Types.LaunchPadLaunchPadRegistrationProjection[], ApiError>> {
    const url = `/v1/launch-pad/events/${eventId}/registrations/management`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LaunchPadLaunchPadRegistrationProjection[], ApiError>;
  }

  /**
   */
  async postLaunchPadEventsSlots(
    eventId: string,
    body: Types.LaunchPadCreateLaunchPadSlotInput,
  ): Promise<Result<Types.LaunchPadLaunchPadSlotProjection, ApiError>> {
    const url = `/v1/launch-pad/events/${eventId}/slots`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadCreateLaunchPadSlotInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadSlotProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putLaunchPadEvents(id: string, body: Types.LaunchPadUpdateLaunchPadEventInput): Promise<Result<Types.LaunchPadLaunchPadEventProjection, ApiError>> {
    const url = `/v1/launch-pad/events/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadUpdateLaunchPadEventInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadEventProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadEventsTransition(
    id: string,
    body: Types.LaunchPadTransitionLaunchPadEventInput,
  ): Promise<Result<Types.LaunchPadLaunchPadEventProjection, ApiError>> {
    const url = `/v1/launch-pad/events/${id}:transition`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadTransitionLaunchPadEventInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadEventProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLaunchPadEventsManagementForGetLaunchPadEventsByIdManagement(id: string): Promise<Result<Types.LaunchPadLaunchPadEventDetailProjection, ApiError>> {
    const url = `/v1/launch-pad/events/${id}/management`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadEventDetailProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLaunchPadEventsAnalytics(): Promise<Result<Types.LaunchPadLaunchPadAnalyticsProjection, ApiError>> {
    const url = '/v1/launch-pad/events/analytics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadAnalyticsProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putLaunchPadEventsApplications(
    applicationId: string,
    body: Types.LaunchPadUpdateLaunchPadApplicationInput,
  ): Promise<Result<Types.LaunchPadLaunchPadApplicationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/applications/${applicationId}`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadUpdateLaunchPadApplicationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadApplicationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadEventsApplicationsReview(
    applicationId: string,
    body: Types.LaunchPadReviewLaunchPadApplicationInput,
  ): Promise<Result<Types.LaunchPadLaunchPadApplicationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/applications/${applicationId}:review`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadReviewLaunchPadApplicationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadApplicationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadEventsApplicationsWithdraw(applicationId: string): Promise<Result<Types.LaunchPadLaunchPadApplicationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/applications/${applicationId}:withdraw`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadApplicationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLaunchPadEventsApplicationsMe(): Promise<Result<Types.LaunchPadLaunchPadApplicationProjection[], ApiError>> {
    const url = '/v1/launch-pad/events/applications/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LaunchPadLaunchPadApplicationProjection[], ApiError>;
  }

  /**
   */
  async getLaunchPadEventsManagementForGetLaunchPadEventsManagement(): Promise<Result<Types.LaunchPadLaunchPadEventProjection[], ApiError>> {
    const url = '/v1/launch-pad/events/management';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LaunchPadLaunchPadEventProjection[], ApiError>;
  }

  /**
   */
  async getLaunchPadEventsPublicForGetLaunchPadEventsPublic(): Promise<Result<Types.LaunchPadLaunchPadEventProjection[], ApiError>> {
    const url = '/v1/launch-pad/events/public';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    return result as Result<Types.LaunchPadLaunchPadEventProjection[], ApiError>;
  }

  /**
   */
  async getLaunchPadEventsPublicForGetLaunchPadEventsPublicById(id: string): Promise<Result<Types.LaunchPadLaunchPadEventDetailProjection, ApiError>> {
    const url = `/v1/launch-pad/events/public/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadEventDetailProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadEventsRegistrationsCancel(registrationId: string): Promise<Result<Types.LaunchPadLaunchPadRegistrationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/registrations/${registrationId}:cancel`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadEventsRegistrationsTransition(
    registrationId: string,
    body: Types.LaunchPadTransitionLaunchPadRegistrationInput,
  ): Promise<Result<Types.LaunchPadLaunchPadRegistrationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/registrations/${registrationId}:transition`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadTransitionLaunchPadRegistrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLaunchPadEventsRegistrationsMe(): Promise<Result<Types.LaunchPadLaunchPadRegistrationProjection[], ApiError>> {
    const url = '/v1/launch-pad/events/registrations/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LaunchPadLaunchPadRegistrationProjection[], ApiError>;
  }

  /**
   */
  async putLaunchPadEventsSlots(
    slotId: string,
    body: Types.LaunchPadCreateLaunchPadSlotInput,
  ): Promise<Result<Types.LaunchPadLaunchPadSlotProjection, ApiError>> {
    const url = `/v1/launch-pad/events/slots/${slotId}`;

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadCreateLaunchPadSlotInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadSlotProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteLaunchPadEventsSlots(slotId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/launch-pad/events/slots/${slotId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postLaunchPadEventsSlotsRegistrations(slotId: string): Promise<Result<Types.LaunchPadLaunchPadRegistrationProjection, ApiError>> {
    const url = `/v1/launch-pad/events/slots/${slotId}/registrations`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPadRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLaunchPadEventsModule(client: ApiClient): LaunchPadEventsModule {
  return new LaunchPadEventsModule(client);
}
