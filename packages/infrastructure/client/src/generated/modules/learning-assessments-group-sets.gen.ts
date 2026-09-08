/**
 * @game-guild/client - LearningAssessmentsGroupSets Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningAssessmentsGroupSetsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getCoursesGroupSets(courseId: string): Promise<Result<Array<Types.LearningAssessmentsGroupSetSummaryDto>, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsGroupSetSummaryDto>, ApiError>;
  }

  /**
   */
  async postCoursesGroupSets(
    courseId: string,
    body: Types.LearningAssessmentsCreateGroupSetInput,
  ): Promise<Result<Types.LearningAssessmentsGroupSetDto, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsCreateGroupSetInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGroupSetDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getCoursesGroupSetsGroups(courseId: string, setId: string): Promise<Result<Array<Types.LearningAssessmentsGroupDetailDto>, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/${setId}/groups`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningAssessmentsGroupDetailDto>, ApiError>;
  }

  /**
   */
  async postCoursesGroupSetsGroups(
    courseId: string,
    setId: string,
    body: Types.LearningAssessmentsCreateGroupInput,
  ): Promise<Result<Types.LearningAssessmentsGroupDto, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/${setId}/groups`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningAssessmentsCreateGroupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGroupDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesGroupSetsGroupsJoin(courseId: string, groupId: string): Promise<Result<Types.LearningAssessmentsGroupMembershipDto, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/groups/${groupId}/join`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGroupMembershipDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postCoursesGroupSetsGroupsMembers(
    courseId: string,
    groupId: string,
    userId: string,
  ): Promise<Result<Types.LearningAssessmentsGroupMembershipDto, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/groups/${groupId}/members/${userId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningAssessmentsGroupMembershipDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteCoursesGroupSetsGroupsMembers(courseId: string, groupId: string, userId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/groups/${groupId}/members/${userId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async deleteCoursesGroupSetsGroupsMembership(courseId: string, groupId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/groups/${groupId}/membership`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createLearningAssessmentsGroupSetsModule(client: ApiClient): LearningAssessmentsGroupSetsModule {
  return new LearningAssessmentsGroupSetsModule(client);
}
