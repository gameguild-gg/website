/**
 * @game-guild/client - TestingLabTestingEventParticipation Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingEventParticipationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTestingEventsFeedback(eventId: string): Promise<Result<Types.TestingLabTestingEventFeedbackReviewProjection[], ApiError>> {
    const url = `/v1/testing/events/${eventId}/feedback`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingEventFeedbackReviewProjection[], ApiError>;
  }

  /**
   */
  async postTestingEventsFeedbackObligationsFeedback(
    obligationId: string,
    body: Types.TestingLabSubmitTestingEventFeedbackInput,
  ): Promise<Result<Types.TestingLabTestingEventFeedbackProjection, ApiError>> {
    const url = `/v1/testing/events/feedback-obligations/${obligationId}/feedback`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabSubmitTestingEventFeedbackInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingEventFeedbackProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTestingEventsFeedbackObligationsMe(query?: { eventId?: string }): Promise<Result<Types.TestingLabTestingFeedbackObligationProjection[], ApiError>> {
    const url = '/v1/testing/events/feedback-obligations/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingFeedbackObligationProjection[], ApiError>;
  }

  /**
   */
  async getTestingEventsFeedbackMe(query?: { eventId?: string }): Promise<Result<Types.TestingLabTestingEventFeedbackProjection[], ApiError>> {
    const url = '/v1/testing/events/feedback/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingEventFeedbackProjection[], ApiError>;
  }

  /**
   */
  async getTestingEventsParticipants(query?: {
    search?: string;
    status?: Types.TestingLabTestingSlotRegistrationStatus;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.TestingLabTestingParticipantDirectoryProjection, ApiError>> {
    const url = '/v1/testing/events/participants';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingParticipantDirectoryProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingEventsRegistrations(registrationId: string): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection, ApiError>> {
    const url = `/v1/testing/events/registrations/${registrationId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSlotRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingEventsRegistrationsCheckIn(registrationId: string): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection, ApiError>> {
    const url = `/v1/testing/events/registrations/${registrationId}:check-in`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSlotRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingEventsRegistrationsCheckOut(registrationId: string): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection, ApiError>> {
    const url = `/v1/testing/events/registrations/${registrationId}:check-out`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSlotRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingEventsRegistrationsComplete(registrationId: string): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection, ApiError>> {
    const url = `/v1/testing/events/registrations/${registrationId}:complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSlotRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingEventsRegistrationsNoShow(registrationId: string): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection, ApiError>> {
    const url = `/v1/testing/events/registrations/${registrationId}:no-show`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSlotRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingEventsRegistrationsTestedProjects(
    registrationId: string,
    body: Types.TestingLabAssignTestingProjectToTesterInput,
  ): Promise<Result<Types.TestingLabTestingFeedbackObligationProjection, ApiError>> {
    const url = `/v1/testing/events/registrations/${registrationId}/tested-projects`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabAssignTestingProjectToTesterInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingFeedbackObligationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTestingEventsRegistrationsMe(query?: { eventId?: string }): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection[], ApiError>> {
    const url = '/v1/testing/events/registrations/me';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingSlotRegistrationProjection[], ApiError>;
  }

  /**
   */
  async getTestingEventsSlotsRegistrations(
    slotId: string,
    query?: { status?: Types.TestingLabTestingSlotRegistrationStatus },
  ): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection[], ApiError>> {
    const url = `/v1/testing/events/slots/${slotId}/registrations`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingSlotRegistrationProjection[], ApiError>;
  }

  /**
   */
  async postTestingEventsSlotsRegistrations(
    slotId: string,
    body: Types.TestingLabRegisterTestingEventSlotInput,
  ): Promise<Result<Types.TestingLabTestingSlotRegistrationProjection, ApiError>> {
    const url = `/v1/testing/events/slots/${slotId}/registrations`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabRegisterTestingEventSlotInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSlotRegistrationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTestingLabTestingEventParticipationModule(client: ApiClient): TestingLabTestingEventParticipationModule {
  return new TestingLabTestingEventParticipationModule(client);
}
