import {
  ValidatorConstraint,
  ValidatorConstraintInterface,
} from 'class-validator';

@ValidatorConstraint({ async: false })
export class isSlug implements ValidatorConstraintInterface {
  validate(text: string) {
    const slugRegex = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
    return typeof text === 'string' && slugRegex.test(text);
  }

  defaultMessage() {
    return 'Text ($value) is not a valid slug';
  }
}
