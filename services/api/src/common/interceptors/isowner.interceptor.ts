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
  EntityClassWithOwnerField,
  IS_OWNER_KEY,
} from '../../auth/decorators/owner.decorator';
import { OwnerEntity } from '../../auth/entities/owner.entity';
import { UserEntity } from '../../user/entities';
import { DataSource } from 'typeorm';

@Injectable()
export class IsOwnerInterceptor implements NestInterceptor {
  constructor(
    private reflector: Reflector,
    private dataSource: DataSource,
  ) {}

  async intercept(
    context: ExecutionContext,
    next: CallHandler,
  ): Promise<Observable<any>> {
    const isOwner = this.reflector.getAllAndOverride<boolean>(IS_OWNER_KEY, [
      context.getHandler(),
      context.getClass(),
    ]);

    if (!isOwner) {
      return next.handle();
    }

    const entityClass = this.reflector.getAllAndOverride<
      EntityClassWithOwnerField<OwnerEntity>
    >(ENTITY_CLASS_KEY, [context.getHandler(), context.getClass()]);

    if (!entityClass) {
      return next.handle();
    }

    const request = context.switchToHttp().getRequest();
    const user: UserEntity = request.user;
    const entityId = request.body.id;

    const repository = this.dataSource.getRepository(entityClass);
    const entity = await repository.findOne({ where: { id: entityId } });

    if (!entity) {
      throw new ForbiddenException('Entity not found');
    }

    if (entity.owner.id !== user.id) {
      throw new ForbiddenException('You are not the owner of this resource');
    }

    return next.handle();
  }
}
