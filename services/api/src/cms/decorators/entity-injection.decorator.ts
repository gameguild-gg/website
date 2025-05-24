import { createParamDecorator, type ExecutionContext } from '@nestjs/common';

export const ContentInjection = createParamDecorator((data: unknown, context: ExecutionContext) => {
  const request = context.switchToHttp().getRequest();
  return request.content;
});
