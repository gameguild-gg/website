import { TypeOrmCrudService } from "@dataui/crud-typeorm";
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { EventEntity } from "./entities/event.entity";


@Injectable()
export class EventService extends TypeOrmCrudService<EventEntity> {
  private readonly logger = new Logger(EventService.name);

  constructor(@InjectRepository(EventEntity) private readonly repository: Repository<EventEntity>) {
    super(repository);
  }
}
