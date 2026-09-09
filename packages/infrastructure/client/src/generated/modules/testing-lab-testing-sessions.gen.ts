/**
 * @game-guild/client - TestingLabTestingSessions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingSessionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTestingAttendanceSessions(): Promise<Result<void, ApiError>> {
    const url = '/v1/testing/attendance/sessions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingPublicSessions(query?: { take?: number }): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = '/v1/testing/public/sessions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }

  /**
   */
  async getTestingSessionsForGetTestingSessions(query?: { skip?: number; take?: number }): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = '/v1/testing/sessions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }

  /**
   */
  async postTestingSessions(body: Types.TestingLabCreateTestingSessionDto): Promise<Result<Types.TestingLabTestingSession, ApiError>> {
    const url = '/v1/testing/sessions';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabCreateTestingSessionDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSessionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getTestingSessionsForGetTestingSessionsById(id: string): Promise<Result<Types.TestingLabTestingSession, ApiError>> {
    const url = `/v1/testing/sessions/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSessionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putTestingSessions(id: string, body: Types.TestingLabTestingSession): Promise<Result<Types.TestingLabTestingSession, ApiError>> {
    const url = `/v1/testing/sessions/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabTestingSessionSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSessionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingSessions(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postTestingSessionsRestore(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${id}:restore`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingSessionsDetails(id: string): Promise<Result<Types.TestingLabTestingSession, ApiError>> {
    const url = `/v1/testing/sessions/${id}/details`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingSessionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingSessionsAttendance(sessionId: string, body: Types.TestingLabUpdateAttendanceDto): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/attendance`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabUpdateAttendanceDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingSessionsProjects(
    sessionId: string,
    query?: { includeInactive?: boolean },
  ): Promise<Result<Array<Types.TestingLabSessionProjectProjection>, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/projects`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabSessionProjectProjection>, ApiError>;
  }

  /**
   */
  async postTestingSessionsProjects(
    sessionId: string,
    body: Types.TestingLabLinkSessionProjectInput,
  ): Promise<Result<Types.TestingLabSessionProjectProjection, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/projects`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabLinkSessionProjectInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabSessionProjectProjectionSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteTestingSessionsProjects(sessionId: string, projectId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/projects/${projectId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingSessionsStatistics(sessionId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/sessions/${sessionId}/statistics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getTestingSessionsByLocation(locationId: string): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = `/v1/testing/sessions/by-location/${locationId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }

  /**
   */
  async getTestingSessionsByManager(managerId: string): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = `/v1/testing/sessions/by-manager/${managerId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }

  /**
   */
  async getTestingSessionsByRequest(testingRequestId: string): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = `/v1/testing/sessions/by-request/${testingRequestId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }

  /**
   */
  async getTestingSessionsByStatus(status: Types.TestingLabSessionStatus): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = `/v1/testing/sessions/by-status/${status}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }

  /**
   */
  async getTestingSessionsSearch(query?: { searchTerm?: string }): Promise<Result<Array<Types.TestingLabTestingSession>, ApiError>> {
    const url = '/v1/testing/sessions/search';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingSession>, ApiError>;
  }
}

export function createTestingLabTestingSessionsModule(client: ApiClient): TestingLabTestingSessionsModule {
  return new TestingLabTestingSessionsModule(client);
}
