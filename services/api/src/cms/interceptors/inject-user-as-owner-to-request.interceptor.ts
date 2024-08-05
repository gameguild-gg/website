// injector to add owner to content creation request body. it assumes the context has user and the content is in the request body

import {
  BadRequestException,
  type CallHandler,
  type ExecutionContext,
  Injectable,
} from '@nestjs/common';
import { UserEntity } from '../../user/entities';
import { WithPermissionsEntity } from '../../auth/entities/with-roles.entity';

@Injectable()
export class InjectUserAsOwnerToRequestInterceptor {
  async intercept(context: ExecutionContext, next: CallHandler) {
    const request = context.switchToHttp().getRequest();
    const user = request.user as UserEntity;
    if (!user) {
      throw new BadRequestException(
        'error.user: User not found in the context, have you missed the AuthUserInterceptor?',
      );
    }
    // content is any object that inherits from WithPermissionsEntity
    const content = request.body as WithPermissionsEntity;

    // // disallow creation with id
    // if (content && content.id) {
    //   throw new BadRequestException(
    //     'error.id: Content id should not be provided in a create request',
    //   );
    // }

    // inject owner and roles
    if (content) {
      request.body.roles = { owner: user.id, editors: [user.id] };
    }

    return next.handle();
  }
}
