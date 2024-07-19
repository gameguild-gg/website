import { createParamDecorator, type ExecutionContext } from '@nestjs/common';
import { UserEntity } from '../../user/entities';

// export const AuthUser = createParamDecorator((_data: unknown, context: ExecutionContext): UserEntity | undefined => {
//   const request = context.switchToHttp().getRequest();
//
//   const user = request.user;
//
//   if (user?.[Symbol.for('isPublic')]) {
//     return undefined;
//   }
//
//   return user;
// });

export const AuthUser = createParamDecorator(
  (data: unknown, context: ExecutionContext) => {
    const request = context.switchToHttp().getRequest();
    const user = request.user;
    if (user?.[Symbol.for('isPublic')]) {
      return undefined;
    }

    return user;
    // const request = context.switchToHttp().getRequest();
    // return request.user; // Assuming you store the user in the request object during authentication
  },
);
