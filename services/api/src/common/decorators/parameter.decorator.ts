import {
  BadRequestException,
  createParamDecorator,
  ExecutionContext,
  Param,
  ParseUUIDPipe,
  PipeTransform,
  UnprocessableEntityException,
  ValidationPipe,
} from '@nestjs/common';
import { Type } from '@nestjs/common/interfaces';
import { UserEntity } from '../../user/entities';
import { WithRolesEntity } from '../../auth/entities/with-roles.entity';
import { plainToClass, plainToInstance } from 'class-transformer';
import { validate } from 'class-validator';
import 'reflect-metadata';

export function UUIDParam(property: string, ...pipes: Array<Type<PipeTransform> | PipeTransform>): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}

export const BodyOwnerInject = (type: Type<any>) =>
  createParamDecorator(async (data: Type<any>, ctx: ExecutionContext) => {
    const request = ctx.switchToHttp().getRequest();

    const body = request.body;
    if (!body) {
      throw new BadRequestException('error.body: Request body not found in the context.');
    }

    // Validate the body using the provided type
    const bodyInstance = plainToInstance(type, body);
    const errors = await validate(bodyInstance);
    if (errors.length) {
      throw new UnprocessableEntityException(errors);
    }

    const user = request.user as UserEntity;
    if (!user) {
      throw new BadRequestException('error.user: User not found in the context, have you missed the AuthUserInterceptor?');
    }

    (body as WithRolesEntity).owner = user;
    (body as WithRolesEntity).editors = [user];

    // Otherwise, return the entire body
    return body;
  })();
