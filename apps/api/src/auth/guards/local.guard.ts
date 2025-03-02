import { ExecutionContext, Injectable, Logger } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { Observable } from 'rxjs';
import { LOCAL_SIGN_IN_STRATEGY_KEY } from '@/auth/auth.constants';
import { IsPublic } from '@/auth/decorators/public.decorator';

@Injectable()
export class LocalGuard extends AuthGuard(LOCAL_SIGN_IN_STRATEGY_KEY) {
  private readonly logger = new Logger(LocalGuard.name);

  constructor(private readonly reflector: Reflector) {
    super();
  }

  public canActivate(context: ExecutionContext): boolean | Promise<boolean> | Observable<boolean> {
    return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  }
}
