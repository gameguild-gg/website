import { Reflector } from '@nestjs/core';
import { ExecutionContext, Injectable } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { IsPublic } from '@/auth/decorators/public.decorator';

@Injectable()
export class RefreshTokenGuard extends AuthGuard(REFRESH_TOKEN_STRATEGY_KEY) {
  constructor(private reflector: Reflector) {
    super();
  }

  canActivate(context: ExecutionContext) {
    return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  }
}
