import { ExecutionContext, Injectable, Logger } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { Observable } from 'rxjs';

import { REFRESH_TOKEN_STRATEGY_KEY } from '@/auth/auth.constants';
import { IsPublic } from '@/auth/decorators/public.decorator';

@Injectable()
export class RefreshTokenGuard extends AuthGuard(REFRESH_TOKEN_STRATEGY_KEY) {
  private readonly logger = new Logger(RefreshTokenGuard.name);

  constructor(private reflector: Reflector) {
    super();
  }

  public canActivate(context: ExecutionContext): boolean | Promise<boolean> | Observable<boolean> {
    return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  }
}
