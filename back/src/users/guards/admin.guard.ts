import {
  CanActivate,
  ExecutionContext,
  ForbiddenException,
  Injectable,
} from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { ROLE_KEY } from '../decorators/role.decorator';
import { UserRole } from '../user-role.enum';
import { UsersService } from '../users.service';

@Injectable()
export class PermissionGuard implements CanActivate {
  constructor(
    private readonly reflector: Reflector,
    private readonly usersService: UsersService,
  ) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const requiredRole = this.reflector.get<UserRole>(
      ROLE_KEY,
      context.getHandler(),
    );

    if (!requiredRole) return true;

    const request = context.switchToHttp().getRequest();
    const userId = request.user.userId;

    const currentUser = await this.usersService.findOne({ id: userId });

    const params = request.params;

    console.log('Current user Id: ', currentUser.id);
    console.log('Current user Role: ', currentUser.role);
    console.log('Target User Id:', params.userId);

    if (currentUser.role === UserRole.ADMIN) return true;
    if (currentUser.id == params.userId) return true;

    throw new ForbiddenException();
  }
}
