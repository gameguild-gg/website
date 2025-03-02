import { Reflector } from '@nestjs/core';
import { ExecutionContext, Injectable, Logger } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { ACCESS_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { IsPublic } from '@/auth/decorators/public.decorator';

@Injectable()
export class AccessTokenGuard extends AuthGuard(ACCESS_TOKEN_STRATEGY_KEY) {
  private readonly logger = new Logger(AccessTokenGuard.name);

  constructor(private reflector: Reflector) {
    super();
  }

  canActivate(context: ExecutionContext) {
    this.logger.log('AccessTokenGuard.canActivate()');

    return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  }
}
