import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { UserEntity } from 'src/user/entities';

export const User = createParamDecorator(
  (data: unknown, ctx: ExecutionContext) => {
    const request = ctx.switchToHttp().getRequest();
    return request.user as UserEntity; // Assumes user is attached to request (e.g., via JWT or session)

    // Usage:
    // async myFunc(
    //   @User user:UserEntity
    //   ...
    // )
    
  },
);
