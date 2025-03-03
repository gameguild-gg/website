import { CustomDecorator, ExecutionContext, SetMetadata } from '@nestjs/common';
import { Reflector } from '@nestjs/core';

import { IS_PUBLIC_KEY } from '../auth.constants';

export const Public = (isPublic = true): CustomDecorator => {
  return SetMetadata(IS_PUBLIC_KEY, isPublic);
};

export const IsPublic = (context: ExecutionContext, reflector: Reflector): boolean => {
  return reflector.getAllAndOverride<boolean>(IS_PUBLIC_KEY, [context.getHandler(), context.getClass()]);
};
