/**
 * @game-guild/client - LearningLti Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class LearningLtiModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getWellKnownJwksJson(): Promise<Result<void, ApiError>> {
    const url = '/.well-known/jwks.json';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postLtiLaunch(): Promise<Result<void, ApiError>> {
    const url = '/lti/launch';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postLtiLogin(): Promise<Result<void, ApiError>> {
    const url = '/lti/login';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postLtiDeployments(body: Types.LearningLtiCreateLtiDeploymentInput): Promise<Result<void, ApiError>> {
    const url = '/v1/lti/deployments';

    // Validate request body
    const validatedBody = safeParse(Types.LearningLtiCreateLtiDeploymentInputSchema, body, 'request');

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
  async postLtiDeploymentsLineItems(id: string, body: Types.LearningLtiCreateLtiLineItemInput): Promise<Result<void, ApiError>> {
    const url = `/v1/lti/deployments/${id}/line-items`;

    // Validate request body
    const validatedBody = safeParse(Types.LearningLtiCreateLtiLineItemInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createLearningLtiModule(client: ApiClient): LearningLtiModule {
  return new LearningLtiModule(client);
}
