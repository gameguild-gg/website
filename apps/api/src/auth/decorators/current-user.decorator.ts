import { createParamDecorator, ExecutionContext } from '@nestjs/common';

export const CurrentUser = createParamDecorator((data: string, context: ExecutionContext) => {
  const request = context.switchToHttp().getRequest();

  const user = request.user;

  return data ? user?.[data] : user;
});
