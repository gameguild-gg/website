import { createParamDecorator, ExecutionContext, BadRequestException, Type } from '@nestjs/common';
import { UserEntity } from '../../user/entities/user.entity';

interface withUser {
    user: UserEntity,
}

export const BodyUserInject = (type: Type<any>) => createParamDecorator(
  (data: unknown, ctx: ExecutionContext) => {
    const request = ctx.switchToHttp().getRequest();
    const user = request.user as UserEntity;
    
    if (!user) {
      throw new BadRequestException(
        'error.user: User not found in the context, have you missed the AuthUserInterceptor?',
      );
    }

    if (request.body) {
      (request.body as withUser).user = user; // Inject the user into the request body
    } else {
      throw new BadRequestException('error.body: Request body not found in the context.');
    }

    return request.body;
  },
);
