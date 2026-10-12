/**
 * @game-guild/client - LaunchPad Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LaunchPadModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getLaunchPadForGetLaunchPad(query?: { status?: Types.LaunchPadLaunchPlanStatus }): Promise<Result<Types.LaunchPadLaunchPlan[], ApiError>> {
    const url = '/v1/launch-pad';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.LaunchPadLaunchPlan[], ApiError>;
  }

  /**
   */
  async postLaunchPad(body: Types.LaunchPadCreateLaunchPlanInput): Promise<Result<void, ApiError>> {
    const url = '/v1/launch-pad';

    // Validate request body
    const validatedBody = safeParse(Types.LaunchPadCreateLaunchPlanInputSchema, body, 'request');

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
  async getLaunchPadForGetLaunchPadById(id: string): Promise<Result<Types.LaunchPadLaunchPlan, ApiError>> {
    const url = `/v1/launch-pad/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPlanSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadPublish(id: string): Promise<Result<Types.LaunchPadLaunchPlan, ApiError>> {
    const url = `/v1/launch-pad/${id}:publish`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPlanSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postLaunchPadChecklistComplete(id: string, itemId: string): Promise<Result<Types.LaunchPadLaunchPlan, ApiError>> {
    const url = `/v1/launch-pad/${id}/checklist/${itemId}:complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPlanSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getLaunchPadProjects(projectId: string): Promise<Result<Types.LaunchPadLaunchPlan, ApiError>> {
    const url = `/v1/launch-pad/projects/${projectId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.LaunchPadLaunchPlanSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createLaunchPadModule(client: ApiClient): LaunchPadModule {
  return new LaunchPadModule(client);
}
