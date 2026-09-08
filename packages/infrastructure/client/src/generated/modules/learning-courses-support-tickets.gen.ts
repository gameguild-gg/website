/**
 * @game-guild/client - LearningCoursesSupportTickets Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningCoursesSupportTicketsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesSupportTicketsForGetCoursesByCourseIdSupportTickets(
    courseId: string,
    query?: { skip?: number; take?: number },
  ): Promise<Result<Types.PagedResultSupportTicketDto, ApiError>> {
    const url = `/v1/courses/${courseId}/support/tickets`;

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
  async getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketId(
    courseId: string,
    ticketId: string,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/courses/${courseId}/support/tickets/${ticketId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
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
  async postCoursesSupportTicketsResolve(
    courseId: string,
    ticketId: string,
    body: Types.LearningCoursesResolveCourseSupportTicketInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/courses/${courseId}/support/tickets/${ticketId}:resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesResolveCourseSupportTicketInputSchema, body, 'request');

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
  async postCoursesSupportTicketsMessages(
    courseId: string,
    ticketId: string,
    body: Types.LearningCoursesCourseSupportTicketMessageInput,
  ): Promise<Result<Types.CommerceProductsSupportTicketDto, ApiError>> {
    const url = `/v1/courses/${courseId}/support/tickets/${ticketId}/messages`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningCoursesCourseSupportTicketMessageInputSchema, body, 'request');

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

export function createLearningCoursesSupportTicketsModule(client: ApiClient): LearningCoursesSupportTicketsModule {
  return new LearningCoursesSupportTicketsModule(client);
}
