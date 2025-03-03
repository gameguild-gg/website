import { Injectable, Logger } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';

import { GOOGLE_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';

@Injectable()
export class GoogleSignInGuard extends AuthGuard(GOOGLE_SIGN_IN_STRATEGY_KEY) {
  private readonly logger = new Logger(GoogleSignInGuard.name);

  constructor(private reflector: Reflector) {
    super();
  }

  // public canActivate(context: ExecutionContext): boolean | Promise<boolean> | Observable<boolean> {
  //   return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  // }
}
