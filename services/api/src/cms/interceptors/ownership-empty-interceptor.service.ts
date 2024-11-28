import { CallHandler, ExecutionContext, Injectable } from '@nestjs/common';

@Injectable()
export class OwnershipEmptyInterceptor {
  intercept(context: ExecutionContext, next: CallHandler) {
    const request = context.switchToHttp().getRequest();

    if (request.body) {
      if (request.body.owner) {
        throw new Error('error.owner: Owner must be empty.');
      }
      if (request.body.editors) {
        throw new Error('error.editors: Editors must be empty.');
      }
    }
    return next.handle();
  }
}
