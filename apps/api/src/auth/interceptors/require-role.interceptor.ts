import {
  Injectable,
  NestInterceptor,
  ExecutionContext,
  CallHandler,
  ForbiddenException,
  InternalServerErrorException,
} from '@nestjs/common';
import { Observable } from 'rxjs';
import { Reflector } from '@nestjs/core';
import { UserEntity } from '../../user/entities';
import { DataSource, ArrayContains } from 'typeorm';
import { ContentUserRolesEnum } from '../auth.enum';
import { WithRolesEntity } from '../entities/with-roles.entity';
import {
  ENTITY_CLASS_KEY,
  REQUIRED_ROLE_KEY,
  EntityClassWithRolesField,
} from '../decorators';

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
    // get required role
    const requiredRole = this.reflector.getAllAndOverride<ContentUserRolesEnum>(
      REQUIRED_ROLE_KEY,
      [context.getHandler(), context.getClass()],
    );

    if (!requiredRole) {
      throw new InternalServerErrorException(
        "Don't use this interceptor without a required role.",
      );
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

    // get uuid from request path. it is the last element in the path
    let entityId = request.path.split('/').pop();
    if (!entityId) entityId = request.body.id;
    if (!entityId) {
      throw new ForbiddenException(
        'Entity id not found in the path or in the body',
      );
    }

    const repository =
      this.dataSource.getRepository<WithRolesEntity>(entityClass);

    let entity: WithRolesEntity;
    if (requiredRole === ContentUserRolesEnum.OWNER) {
      entity = await repository.findOne({
        where: {
          id: entityId,
          owner: user,
        },
      });
    } else if (requiredRole === ContentUserRolesEnum.EDITOR) {
      entity = await repository.findOne({
        where: {
          id: entityId,
          editors: { id: user.id },
        },
      });
    }

    if (!entity) {
      throw new ForbiddenException(
        'Content not found, or you do not have access to it.',
      );
    }

    // store the content into the request
    request.content = entity;

    return next.handle();
  }
}
