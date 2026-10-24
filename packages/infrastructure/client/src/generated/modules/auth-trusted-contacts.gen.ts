/**
 * @game-guild/client - AuthTrustedContacts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthTrustedContactsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists the current user's trusted contacts (active invites and confirmed contacts).
   *
   * Returns the acting user's trusted contacts for account recovery. Never includes invite tokens.
   */
  async getAuthTrustedContacts(): Promise<Result<Types.IdentityAuthenticationTrustedContactDto[], ApiError>> {
    const url = '/v1/auth/trusted-contacts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthenticationTrustedContactDto[], ApiError>;
  }

  /**
   * Designates a trusted contact and emails them a single-use invite.
   *
   * Designates a trusted recovery contact for the acting user and sends a single-use confirmation invite. The contact must confirm before receiving recovery codes.
   */
  async postAuthTrustedContacts(
    body: Types.IdentityAuthenticationAddTrustedContactInput,
  ): Promise<Result<Types.IdentityAuthenticationTrustedContactActionResult, ApiError>> {
    const url = '/v1/auth/trusted-contacts';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationAddTrustedContactInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationTrustedContactActionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Removes (revokes) one of the current user's trusted contacts.
   *
   * Revokes one of the acting user's trusted contacts. The contact immediately stops counting toward recovery thresholds.
   */
  async deleteAuthTrustedContacts(contactId: string): Promise<Result<Types.IdentityAuthenticationTrustedContactActionResult, ApiError>> {
    const url = `/v1/auth/trusted-contacts/${contactId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationTrustedContactActionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Redeems a trusted-contact invite token; the contact confirms participation.
   *
   * Consumes the single-use, expiring invite token emailed to the contact. Denials are generic and never reveal token state.
   */
  async postAuthTrustedContactsConfirm(
    body: Types.IdentityAuthenticationConfirmTrustedContactInput,
  ): Promise<Result<Types.IdentityAuthenticationTrustedContactConfirmResult, ApiError>> {
    const url = '/v1/auth/trusted-contacts/confirm';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationConfirmTrustedContactInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationTrustedContactConfirmResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthTrustedContactsModule(client: ApiClient): AuthTrustedContactsModule {
  return new AuthTrustedContactsModule(client);
}
