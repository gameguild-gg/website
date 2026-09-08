/**
 * @game-guild/client - LearningWorkspacesLearnerWorkspace Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningWorkspacesLearnerWorkspaceModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getLearningCoursesWorkspace(courseId: string): Promise<Result<Types.LearningWorkspacesLearnerCourseWorkspaceDto, ApiError>> {
    const url = `/v1/learning/courses/${courseId}/workspace`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningWorkspacesLearnerCourseWorkspaceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningMeDashboard(): Promise<Result<Types.LearningWorkspacesLearnerDashboardDto, ApiError>> {
    const url = '/v1/learning/me/dashboard';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LearningWorkspacesLearnerDashboardDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLearningMeSearch(query?: { q?: string; take?: number }): Promise<Result<Array<Types.LearningWorkspacesLearnerSearchResultDto>, ApiError>> {
    const url = '/v1/learning/me/search';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.LearningWorkspacesLearnerSearchResultDto>, ApiError>;
  }
}

export function createLearningWorkspacesLearnerWorkspaceModule(client: ApiClient): LearningWorkspacesLearnerWorkspaceModule {
  return new LearningWorkspacesLearnerWorkspaceModule(client);
}
