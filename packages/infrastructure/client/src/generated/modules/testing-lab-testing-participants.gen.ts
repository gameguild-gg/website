/**
 * @game-guild/client - TestingLabTestingParticipants Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingParticipantsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTestingAttendanceStudents(): Promise<Result<void, ApiError>> {
    const url = '/v1/testing/attendance/students';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingRequestsParticipants(requestId: string): Promise<Result<Types.TestingLabTestingParticipant[], ApiError>> {
    const url = `/v1/testing/requests/${requestId}/participants`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabTestingParticipant[], ApiError>;
  }

  /**
   */
  async postTestingRequestsParticipants(requestId: string, userId: string): Promise<Result<Types.TestingLabTestingParticipantMutationProjection, ApiError>> {
    const url = `/v1/testing/requests/${requestId}/participants/${userId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingParticipantMutationProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingRequestsParticipants(requestId: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/requests/${requestId}/participants/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingRequestsParticipantsCheck(requestId: string, userId: string): Promise<Result<boolean, ApiError>> {
    const url = `/v1/testing/requests/${requestId}/participants/${userId}/check`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   */
  async postTestingSessionsRegister(
    sessionId: string,
    body: Types.TestingLabSessionRegistrationInput,
  ): Promise<Result<Types.TestingLabSessionRegistration, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/register`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabSessionRegistrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabSessionRegistrationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingSessionsRegister(sessionId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/register`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingSessionsRegistrations(sessionId: string): Promise<Result<Types.TestingLabSessionRegistration[], ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/registrations`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabSessionRegistration[], ApiError>;
  }

  /**
   */
  async getTestingSessionsWaitlist(sessionId: string): Promise<Result<Types.TestingLabSessionWaitlist[], ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/waitlist`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.TestingLabSessionWaitlist[], ApiError>;
  }

  /**
   */
  async postTestingSessionsWaitlist(
    sessionId: string,
    body: Types.TestingLabSessionRegistrationInput,
  ): Promise<Result<Types.TestingLabSessionWaitlist, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/waitlist`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabSessionRegistrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabSessionWaitlistSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingSessionsWaitlist(sessionId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/waitlist`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingUsersActivity(userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/users/${userId}/activity`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTestingLabTestingParticipantsModule(client: ApiClient): TestingLabTestingParticipantsModule {
  return new TestingLabTestingParticipantsModule(client);
}
