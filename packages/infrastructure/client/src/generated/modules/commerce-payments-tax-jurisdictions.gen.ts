/**
 * @game-guild/client - CommercePaymentsTaxJurisdictions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsTaxJurisdictionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all tax jurisdictions
   */
  async getTaxJurisdictionsForGetTaxJurisdictions(): Promise<Result<Types.CommercePaymentsTaxRate[], ApiError>> {
    const url = '/api/v1/tax-jurisdictions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsTaxRate[], ApiError>;
  }

  /**
   * Create a new tax jurisdiction
   *
   * Creates a new tax jurisdiction with the provided information.
   */
  async postTaxJurisdictions(body: Types.CommercePaymentsCreateTaxJurisdictionInput): Promise<Result<void, ApiError>> {
    const url = '/api/v1/tax-jurisdictions';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsCreateTaxJurisdictionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tax jurisdiction by ID
   *
   * Retrieves detailed information for a specific tax jurisdiction.
   */
  async getTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionId(jurisdictionId: string): Promise<Result<Types.CommercePaymentsTaxJurisdictionDto, ApiError>> {
    const url = `/api/v1/tax-jurisdictions/${jurisdictionId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsTaxJurisdictionDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a tax jurisdiction
   *
   * Deletes a tax jurisdiction by ID.
   */
  async deleteTaxJurisdictions(jurisdictionId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/tax-jurisdictions/${jurisdictionId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update a tax jurisdiction
   *
   * Updates specific fields of a tax jurisdiction.
   */
  async patchTaxJurisdictions(jurisdictionId: string, body: Types.CommercePaymentsPatchTaxJurisdictionInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/tax-jurisdictions/${jurisdictionId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPatchTaxJurisdictionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommercePaymentsTaxJurisdictionsModule(client: ApiClient): CommercePaymentsTaxJurisdictionsModule {
  return new CommercePaymentsTaxJurisdictionsModule(client);
}
