// returns an array of objects with key value pairs of error messages
import { Api } from '@game-guild/apiclient';

// retruns an array of object which are key value pairs of error messages
export function getConstraintsFromUnprocessableEntityError<T>(
  error: Api.ApiErrorResponseDto,
  type: keyof T,
): { [key: string]: string }[] {
  // iterate over all errors in the message
  for (const m of error.message) {
    // if the key matches the type, return the error message
    if (m.property === type) {
      return m.constraints;
    }
  }
  return [];
}
