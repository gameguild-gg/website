/**
 * @game-guild/client - SocialReactions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class SocialReactionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async putApiSocialReactions(body: Types.SocialReactionsSetReactionInput): Promise<Result<Types.SocialReactionsReactionDto, ApiError>> {
    const url = '/api/social/reactions';

    // Validate request body
    const validatedBody = safeParse(Types.SocialReactionsSetReactionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialReactionsReactionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteApiSocialReactions(body: Types.SocialReactionsRemoveReactionInput): Promise<Result<void, ApiError>> {
    const url = '/api/social/reactions';

    // Validate request body
    const validatedBody = safeParse(Types.SocialReactionsRemoveReactionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiSocialReactionsMeTarget(
    targetType: Types.SocialReactionsReactionTargetType,
    targetId: string,
  ): Promise<Result<Types.SocialReactionsReactionDto, ApiError>> {
    const url = `/api/social/reactions/me/target/${targetType}/${targetId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialReactionsReactionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiSocialReactionsTarget(
    targetType: Types.SocialReactionsReactionTargetType,
    targetId: string,
  ): Promise<Result<Types.SocialReactionsTargetReactionSummaryDto, ApiError>> {
    const url = `/api/social/reactions/target/${targetType}/${targetId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.SocialReactionsTargetReactionSummaryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createSocialReactionsModule(client: ApiClient): SocialReactionsModule {
  return new SocialReactionsModule(client);
}
