import { CallHandler, ExecutionContext, Injectable, Logger, NestInterceptor } from '@nestjs/common';
import { Observable } from 'rxjs';

@Injectable()
export class AuthenticatedUserInterceptor implements NestInterceptor {
  private readonly logger = new Logger(AuthenticatedUserInterceptor.name);

  intercept(context: ExecutionContext, next: CallHandler): Observable<any> {
    this.logger.log('AuthenticatedUserInterceptor.intercept()');
    // const request = context.switchToHttp().getRequest();
    //
    // const user = <UserEntity>request.user;
    // ContextProvider.setAuthenticatedUser(user);
    return next.handle();
  }
}
