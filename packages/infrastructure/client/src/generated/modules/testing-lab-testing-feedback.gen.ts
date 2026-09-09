/**
 * @game-guild/client - TestingLabTestingFeedback Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TestingLabTestingFeedbackModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTestingFeedback(query?: {
    Search?: string;
    Source?: Types.TestingLabTestingFeedbackSource;
    EventId?: string;
    RequestId?: string;
    UserId?: string;
    Reported?: boolean;
    Quality?: Types.TestingLabFeedbackQuality;
    Skip?: number;
    Take?: number;
  }): Promise<Result<Types.TestingLabTestingFeedbackDirectoryPage, ApiError>> {
    const url = '/v1/testing/feedback';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingFeedbackDirectoryPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postTestingFeedback(body: Types.TestingLabSubmitFeedbackDto): Promise<Result<void, ApiError>> {
    const url = '/v1/testing/feedback';

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabSubmitFeedbackDtoSchema, body, 'request');

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
  async postTestingFeedbackQuality(feedbackId: string, body: Types.TestingLabRateFeedbackQualityDto): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/feedback/${feedbackId}/quality`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabRateFeedbackQualityDtoSchema, body, 'request');

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
  async postTestingFeedbackReport(feedbackId: string, body: Types.TestingLabReportFeedbackDto): Promise<Result<void, ApiError>> {
    const url = `/v1/testing/feedback/${feedbackId}/report`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabReportFeedbackDtoSchema, body, 'request');

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
  async getTestingFeedbackByUser(userId: string): Promise<Result<Array<Types.TestingLabTestingFeedback>, ApiError>> {
    const url = `/v1/testing/feedback/by-user/${userId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingFeedback>, ApiError>;
  }

  /**
   */
  async getTestingRequestsFeedback(requestId: string): Promise<Result<Array<Types.TestingLabTestingFeedback>, ApiError>> {
    const url = `/v1/testing/requests/${requestId}/feedback`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.TestingLabTestingFeedback>, ApiError>;
  }

  /**
   */
  async postTestingRequestsFeedback(requestId: string, body: Types.TestingLabFeedbackInput): Promise<Result<Types.TestingLabTestingFeedback, ApiError>> {
    const url = `/v1/testing/requests/${requestId}/feedback`;

    // Validate request body
    const validatedBody = safeParse(Types.TestingLabFeedbackInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.TestingLabTestingFeedbackSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTestingLabTestingFeedbackModule(client: ApiClient): TestingLabTestingFeedbackModule {
  return new TestingLabTestingFeedbackModule(client);
}
