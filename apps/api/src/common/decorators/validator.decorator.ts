import {
  IsLowercase,
  IsNotEmpty,
  IsString,
  IsStrongPassword,
  Matches,
  MaxLength,
  MinLength,
  IsEmail as IsEmailCV,
  ValidationOptions,
  registerDecorator,
  ValidateIf,
  IsPhoneNumber as IsPhoneNumberCV,
} from 'class-validator';

export function IsEmail(): PropertyDecorator {
  return (target, key) => {
    IsNotEmpty({
      message: `error.isNotEmpty: ${String(key)} must not be empty.`,
    })(target, key);
    IsString({
      message: `error.invalidString: ${String(key)} must be a string.`,
    })(target, key);
    IsLowercase({
      message: `error.invalidCase: ${String(key)} must be lowercase.`,
    })(target, key);
    MaxLength(254, {
      message: `error.invalidLength: ${String(key)} must be shorter than or equal to 254 characters.`,
    })(target, key);
    IsEmailCV(
      {},
      {
        message: 'error.invalidEmail: It must be a valid email address.',
      },
    )(target, key);
  };
}

export function IsUsername(): PropertyDecorator {
  return (target, key) => {
    IsString({
      message: `error.invalidString: ${String(key)} must be a string.`,
    })(target, key);
    IsNotEmpty({
      message: `error.isNotEmpty: ${String(key)} must not be empty.`,
    })(target, key);
    MaxLength(32, {
      message: `error.invalidLength: ${String(key)} must be shorter than or equal to 32 characters.`,
    })(target, key);
    Matches(/^[a-z0-9_.-]{1,32}$/, {
      message: `error.invalidPattern: ${String(key)} must contain only alphanumeric characters, underscores, periods, hyphens, and be shorter than or equal to 32 characters.`,
    })(target, key);
  };
}

export function IsPassword(): PropertyDecorator {
  return (target, key) => {
    IsString({
      message: `error.invalidString: ${String(key)} must be a string.`,
    })(target, key);
    IsNotEmpty({
      message: `error.isNotEmpty: ${String(key)} must not be empty.`,
    })(target, key);
    MaxLength(64, {
      message: `error.invalidLength: ${String(key)} must be shorter than or equal to 64 characters.`,
    })(target, key);
    MinLength(8, {
      message: `error.invalidLength: ${String(key)} must be longer than or equal to 8 characters.`,
    })(target, key);
    IsStrongPassword(
      {
        minLength: 8,
        minLowercase: 1,
        minUppercase: 1,
        minNumbers: 1,
        minSymbols: 1,
      },
      {
        message: `error.strength: ${String(key)} is too weak. It must have at least 8 characters, including 1 lowercase, 1 uppercase, 1 number, and 1 symbol.`,
      },
    )(target, key);
  };
}

export function IsPhoneNumber(
  validationOptions?: ValidationOptions & {
    region?: Parameters<typeof IsPhoneNumberCV>[0];
  },
): PropertyDecorator {
  return IsPhoneNumberCV(validationOptions?.region, {
    message: 'error.phoneNumber',
    ...validationOptions,
  });
}

export function IsUndefinable(options?: ValidationOptions): PropertyDecorator {
  return ValidateIf((_obj, value) => value !== undefined, options);
}

export function IsNullable(options?: ValidationOptions): PropertyDecorator {
  return ValidateIf((_obj, value) => value !== null, options);
}

export function IsIntegerNumber(
  validationOptions?: ValidationOptions,
): PropertyDecorator {
  return (target: any, propertyKey: string | symbol) => {
    registerDecorator({
      target: target.constructor,
      propertyName: propertyKey as string,
      options: validationOptions,
      constraints: [],
      validator: {
        validate(value: any) {
          return !(typeof value !== 'number' || !Number.isInteger(value));
        },
        defaultMessage() {
          return 'The value must be an integer.';
        },
      },
    });
  };
}
