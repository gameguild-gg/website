import { BadRequestException, Injectable, PipeTransform } from '@nestjs/common';

@Injectable()
export class ExcludeFieldsPipe<T extends object> implements PipeTransform {
  constructor(private readonly fieldsToExclude: (keyof T)[]) {}

  transform(value: T) {
    for (const field of this.fieldsToExclude) {
      if (field in value) {
        throw new BadRequestException(`Field "${String(field)}" should not be present`);
      }
    }
    return value;
  }
}
