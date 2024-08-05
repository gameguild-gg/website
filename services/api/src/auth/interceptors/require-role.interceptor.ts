import {
  Injectable,
  NestInterceptor,
  ExecutionContext,
  CallHandler,
  ForbiddenException,
} from '@nestjs/common';
import { Observable } from 'rxjs';
import { Reflector } from '@nestjs/core';
import {
  ENTITY_CLASS_KEY,
  EntityClassWithRolesField,
  REQUIRED_ROLE_KEY,
} from '../decorators/has-role.decorator';
import { UserEntity } from '../../user/entities';
import { ArrayContains, DataSource } from 'typeorm';
import { ContentUserRolesEnum } from '../auth.enum';
import { WithPermissionsEntity } from '../entities/with-roles.entity';

@Injectable()
export class RequireRoleInterceptor implements NestInterceptor {
  constructor(
    private reflector: Reflector,
    private dataSource: DataSource,
  ) {}

  async intercept(
    context: ExecutionContext,
    next: CallHandler,
  ): Promise<Observable<any>> {
    const requiredRole = this.reflector.getAllAndOverride<ContentUserRolesEnum>(
      REQUIRED_ROLE_KEY,
      [context.getHandler(), context.getClass()],
    );

    if (!requiredRole) {
      return next.handle();
    }

    const entityClass = this.reflector.getAllAndOverride<
      EntityClassWithRolesField<any>
    >(ENTITY_CLASS_KEY, [context.getHandler(), context.getClass()]);

    // reject
    if (!entityClass) {
      throw new ForbiddenException('Entity class not found.');
    }

    const request = context.switchToHttp().getRequest();
    const user: UserEntity = request.user;
    const entityId = request.body.id;

    const repository =
      this.dataSource.getRepository<WithPermissionsEntity>(entityClass);

    let entity: WithPermissionsEntity;
    if (requiredRole === ContentUserRolesEnum.OWNER) {
      entity = await repository.findOne({
        where: {
          id: entityId,
          roles: { owner: user.id },
        },
        select: { id: true },
      });
    } else if (requiredRole === ContentUserRolesEnum.EDITOR) {
      entity = await repository.findOne({
        where: {
          id: entityId,
          roles: { editors: ArrayContains([user.id]) },
        },
        select: { id: true },
      });
    }

    if (!entity) {
      throw new ForbiddenException(
        'Content not found, or you do not have access to it.',
      );
    }

    return next.handle();
  }
}
