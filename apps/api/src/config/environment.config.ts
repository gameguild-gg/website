import { config } from 'dotenv';

config();

export const environment = {
  get: (key: string, defaultValue?: string): string => {
    const value = process.env[key];

    if (value === undefined || value === null || value.trim() === '') {
      if (defaultValue !== undefined) return defaultValue;

      throw new Error(`${key} environment variable is not defined`);
    }

    return value;
  },
  getString: (key: string, defaultValue?: string): string => {
    const value = environment.get(key, defaultValue);

    return value.replace(/\\n/g, '\n');
  },
  getNumber: (key: string, defaultValue?: number): number => {
    const rawValue = defaultValue !== undefined ? environment.get(key, defaultValue?.toString()) : environment.get(key);

    const value = Number(rawValue);

    if (isNaN(value)) {
      throw new Error(`${key} environment variable is not a valid number`);
    }

    return value;
  },
  getBoolean: (key: string, defaultValue?: boolean): boolean => {
    const rawValue = environment.get(key, defaultValue !== undefined ? defaultValue.toString() : undefined).toLowerCase();

    if (rawValue === 'true') return true;
    if (rawValue === 'false') return false;

    throw new Error(`${key} environment variable is not a boolean (expected "true" or "false").`);
  },
};
