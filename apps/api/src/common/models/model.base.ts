import { AggregateRoot } from '@nestjs/cqrs';
import { EntityDto } from '@/common/dtos/entity.dto';

export abstract class ModelBase extends AggregateRoot implements EntityDto {
  public readonly id: string;
  public readonly createdAt: Date;
  public readonly updatedAt: Date;
}
