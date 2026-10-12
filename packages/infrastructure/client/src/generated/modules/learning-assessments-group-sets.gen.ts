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
   * List a course's group sets with per-group summaries. Open to any active course member.
   */
  async getCoursesGroupSets(courseId: string): Promise<Result<Types.LearningAssessmentsGroupSetSummaryDto[], ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsGroupSetSummaryDto[], ApiError>;
  }

  /**
   * Create a group set for a course. Instructor only.
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
   * List the groups of one group set with member display names. Open to any active course member.
   */
  async getCoursesGroupSetsGroups(courseId: string, setId: string): Promise<Result<Types.LearningAssessmentsGroupDetailDto[], ApiError>> {
    const url = `/v1/courses/${courseId}/group-sets/${setId}/groups`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.LearningAssessmentsGroupDetailDto[], ApiError>;
  }

  /**
   * Create a group inside a group set. Instructor only.
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
   * Student self-signup into a group.
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
   * Instructor manual add of a user to a group (bypasses the lock-at-due rule, not capacity).
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
   * Instructor manual remove of a member from a group (bypasses the lock-at-due rule).
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
   * Student leaves their own membership in a group.
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
