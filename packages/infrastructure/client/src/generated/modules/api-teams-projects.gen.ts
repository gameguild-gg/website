/**
 * @game-guild/client - ApiTeamsProjects Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class ApiTeamsProjectsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getTeamsProjects(teamId: string): Promise<Result<Types.APITeamsTeamProjectSummary[], ApiError>> {
    const url = `/v1/teams/${teamId}/projects`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.APITeamsTeamProjectSummary[], ApiError>;
  }
}

export function createApiTeamsProjectsModule(client: ApiClient): ApiTeamsProjectsModule {
  return new ApiTeamsProjectsModule(client);
}
