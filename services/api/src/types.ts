/* eslint-disable @typescript-eslint/no-explicit-any */
export type Constructor<T = any, Arguments extends unknown[] = any[]> = new (
  ...arguments_: Arguments
) => T;

export type KeyOfType<Entity, U> = {
  [P in keyof Required<Entity>]: Required<Entity>[P] extends U
    ? P
    : Required<Entity>[P] extends U[]
      ? P
      : never;
}[keyof Entity];

// Does NOT act like an interface when used with `implements`:
// It's a type alias that picks only the fields in `K`, but won't enforce implementation.
export type PickFields<T, K extends keyof T> = Pick<T, K>;

// Acts like an interface when used with `implements`:
// Enforces the structure by including ONLY the fields specified in `K`.
export type IPickFields<T, K extends keyof T> = {
  [P in K]-?: T[P]; // Ensure required fields are picked.
};

// Does NOT require implementation when used with `implements`:
// Specifies the shape without enforcing explicit field declarations.
export type OmitFields<T, K extends keyof T> = {
  [P in Exclude<keyof T, K>]: T[P];
};

// REQUIRES implementation when used with `implements`:
// Enforces explicit implementation of all fields NOT in `K`.
export type IOmitFields<T, K extends keyof T> = {
  [P in Exclude<keyof T, K>]-?: T[P]; // Use `-?` to make fields strictly required.
};

// todo: revisit this type, I am pretty sure everything using it, doest not work as expected
export type PartialWithoutFields<T, K extends keyof T> = Partial<
  OmitFields<T, K>
>;

export type StrictInterface<T> = {
  [K in keyof T]: T[K];
} & {
  [key: string]: never; // Allow no extra keys.
};
