import {
  BadRequestException,
  createParamDecorator,
  ExecutionContext,
  Param,
  ParseUUIDPipe,
  PipeTransform,
} from '@nestjs/common';
import { Type } from '@nestjs/common/interfaces';
import { UserEntity } from '../../user/entities';
import { WithRolesEntity } from '../../auth/entities/with-roles.entity';

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}

export const BodyOwnerInject = createParamDecorator(
  (data: unknown, ctx: ExecutionContext) => {
    const request = ctx.switchToHttp().getRequest();

    const user = request.user as UserEntity;
    if (!user) {
      throw new BadRequestException(
        'error.user: User not found in the context, have you missed the AuthUserInterceptor?',
      );
    }

    if (request.body) {
      (request.body as WithRolesEntity).owner = user;
      (request.body as WithRolesEntity).editors = [user];
    } else
      throw new BadRequestException(
        'error.body: Request body not found in the context.',
      );

    // Otherwise, return the entire body
    return request.body;
  },
);
