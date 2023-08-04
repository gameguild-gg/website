import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable } from '@nestjs/common';
import { EventEntity } from './event.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';

@Injectable()
export class EventService extends TypeOrmCrudService<EventEntity> {
    constructor(
        @InjectRepository(EventEntity)
        private repository: Repository<EventEntity>,
    ) {
        super(repository);
    }
}
