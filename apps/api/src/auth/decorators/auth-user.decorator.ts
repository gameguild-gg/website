import { createParamDecorator, type ExecutionContext } from '@nestjs/common';

export const AuthUser = createParamDecorator(
  (data: unknown, context: ExecutionContext) => {
    const request = context.switchToHttp().getRequest();
    const user = request.user;
    if (user?.[Symbol.for('isPublic')]) {
      // todo: I think this is unnecessary, if not, please remove this comment and the debugger. if the dev adds the AuthUser, it should throw correctly if it doesnt find the user, and do not return undefined
      debugger;
      return undefined;
    }

    return user;
  },
);
