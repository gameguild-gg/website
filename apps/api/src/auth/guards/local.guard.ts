import { ExecutionContext, Injectable } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { LOCAL_STRATEGY_KEY } from '@/auth/auth.constants';
import { CommandBus } from '@nestjs/cqrs';
import { IsPublic } from '@/auth/decorators/public.decorator';
import { Observable } from 'rxjs';

@Injectable()
export class LocalGuard extends AuthGuard(LOCAL_STRATEGY_KEY) {
  constructor(
    private readonly reflector: Reflector,
    private readonly commandBus: CommandBus,
  ) {
    super();
  }

  public canActivate(context: ExecutionContext): boolean | Promise<boolean> | Observable<boolean> {
    // const user = await this.commandBus.execute(ValidateLocalSignInCommand);
    return IsPublic(context, this.reflector) ? true : super.canActivate(context);
  }
}
