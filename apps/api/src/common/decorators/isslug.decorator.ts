import { ValidationOptions, registerDecorator } from 'class-validator';
import { isSlug } from '../validators/IsSlug.validator';

export function IsSlug(validationOptions?: ValidationOptions) {
  return function (object: NonNullable<unknown>, propertyName: string) {
    registerDecorator({
      target: object.constructor,
      propertyName: propertyName,
      options: validationOptions,
      constraints: [],
      validator: isSlug,
    });
  };
}
