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
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * LTI 1.3 launch: the platform form-POSTs the signed id_token here.
   * id_token in the query string is rejected outright (leaks into logs/history).
   */
  async postLtiLaunch(): Promise<Result<void, ApiError>> {
    const url = '/lti/launch';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: false,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * OIDC third-party-initiated login. Validates the platform against registered
   * active deployments, then redirects to the deployment's configured authorization
   * endpoint with state+nonce. All redirect targets come from admin-configured
   * deployment records — never from request input.
   */
  async postLtiLogin(): Promise<Result<void, ApiError>> {
    const url = '/lti/login';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: false,
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
