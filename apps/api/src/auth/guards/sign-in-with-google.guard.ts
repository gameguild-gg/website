import { ExecutionContext, Injectable, Logger } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { Observable } from 'rxjs';
import { SIGN_IN_WITH_GOOGLE_STRATEGY_KEY } from '@/auth/auth.constants';
import { IsPublic } from '@/auth/decorators/public.decorator';

@Injectable()
export class SignInWithGoogleGuard extends AuthGuard(SIGN_IN_WITH_GOOGLE_STRATEGY_KEY) {
  private readonly logger = new Logger(SignInWithGoogleGuard.name);

  constructor(private reflector: Reflector) {
    super();
  }

  public canActivate(context: ExecutionContext): boolean | Promise<boolean> | Observable<boolean> {
    return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  }
}
