/**
 * Zod Validation Examples
 * 
 * This file demonstrates how to use the generated Zod schemas for runtime validation.
 */

import { z } from 'zod';
import {
  type CommercePaymentsTaxJurisdictionDto as Commerce_Payments_TaxJurisdictionDto,
  CommercePaymentsTaxJurisdictionDtoSchema as Commerce_Payments_TaxJurisdictionDtoSchema,
  APIControllersHealthinessOutput,
  APIControllersHealthinessOutputSchema,
} from '../src/generated/types.gen.js';

/**
 * Example 1: Validate data from external API
 */
export function validateTaxJurisdiction(data: unknown): Commerce_Payments_TaxJurisdictionDto {
  // This will throw a ZodError if validation fails
  return Commerce_Payments_TaxJurisdictionDtoSchema.parse(data);
}

/**
 * Example 2: Safe validation with error handling
 */
export function safeParseTaxJurisdiction(data: unknown) {
  const result = Commerce_Payments_TaxJurisdictionDtoSchema.safeParse(data);
  
  if (result.success) {
    console.log('Valid data:', result.data);
    return result.data;
  } else {
    console.error('Validation errors:', result.error.issues);
    return null;
  }
}

/**
 * Example 3: Validate nested objects with arrays
 */
export function validateHealthStatus(data: unknown): APIControllersHealthinessOutput {
  return APIControllersHealthinessOutputSchema.parse(data);
}

/**
 * Example 4: Partial validation - validate only specific fields
 */
export function validateTaxJurisdictionPartial(data: unknown) {
  if (!(Commerce_Payments_TaxJurisdictionDtoSchema instanceof z.ZodObject)) {
    throw new Error('Tax jurisdiction partial validation requires an object schema');
  }
  const PartialSchema = Commerce_Payments_TaxJurisdictionDtoSchema.partial();
  return PartialSchema.parse(data);
}

/**
 * Example 5: Validate and transform data
 */
export function validateAndTransformTaxJurisdiction(data: unknown) {
  const result = Commerce_Payments_TaxJurisdictionDtoSchema.transform((val) => ({
    ...val,
    // Add computed fields
    displayName: val.name || val.code || 'Unknown',
    isValid: val.isActive ?? true,
  })).parse(data);
  
  return result;
}

/**
 * Example 6: Create custom validation with refinements
 */
export const CustomTaxJurisdictionSchema = Commerce_Payments_TaxJurisdictionDtoSchema
  .refine((data) => {
    // Custom validation: if active, must have a name
    if (data.isActive && !data.name) {
      return false;
    }
    return true;
  }, {
    message: 'Active tax jurisdictions must have a name',
    path: ['name'],
  })
  .refine((data) => {
    // Custom validation: default rate must be between 0 and 100
    if (data.defaultRate != null && (data.defaultRate < 0 || data.defaultRate > 100)) {
      return false;
    }
    return true;
  }, {
    message: 'Default rate must be between 0 and 100',
    path: ['defaultRate'],
  });

/**
 * Example 7: Infer TypeScript types from Zod schemas
 * This is useful when you want to work with the validated type
 */
export type ValidatedTaxJurisdiction = z.infer<typeof Commerce_Payments_TaxJurisdictionDtoSchema>;
export type CustomValidatedTaxJurisdiction = z.infer<typeof CustomTaxJurisdictionSchema>;

/**
 * Example 8: Validate API responses
 */
export async function fetchAndValidateTaxJurisdiction(id: string): Promise<Commerce_Payments_TaxJurisdictionDto> {
  // Simulate API call
  const response = await fetch(`/api/tax-jurisdictions/${id}`);
  const data = await response.json();
  
  // Validate the response data
  return Commerce_Payments_TaxJurisdictionDtoSchema.parse(data);
}

/**
 * Example 9: Validate form submissions
 */
export function validateFormData(formData: FormData) {
  const data = {
    code: formData.get('code'),
    name: formData.get('name'),
    country: formData.get('country'),
    defaultRate: parseFloat(formData.get('defaultRate') as string),
    isActive: formData.get('isActive') === 'true',
  };
  
  return Commerce_Payments_TaxJurisdictionDtoSchema.parse(data);
}

/**
 * Example 10: Validate arrays of objects
 */
export function validateTaxJurisdictions(data: unknown) {
  const ArraySchema = z.array(Commerce_Payments_TaxJurisdictionDtoSchema);
  return ArraySchema.parse(data);
}
