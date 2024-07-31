import { Param, ParseUUIDPipe, PipeTransform } from '@nestjs/common';
import { Type } from '@nestjs/common/interfaces';

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
