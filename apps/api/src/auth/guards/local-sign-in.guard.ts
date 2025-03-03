import { Injectable, Logger } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { LOCAL_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';

@Injectable()
export class LocalSignInGuard extends AuthGuard(LOCAL_SIGN_IN_STRATEGY_KEY) {
  private readonly logger = new Logger(LocalSignInGuard.name);

  constructor(private readonly reflector: Reflector) {
    super();
  }

  // public canActivate(context: ExecutionContext): boolean | Promise<boolean> | Observable<boolean> {
  //   return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  // }
}
