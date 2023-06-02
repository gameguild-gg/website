import { Controller } from '@nestjs/common';
import { EventService } from './event.service';
import { Crud, CrudController } from '@dataui/crud';
import { EventEntity } from './event.entity';
import { ApiTags } from '@nestjs/swagger';
import { EventTypeEnum } from './event.enum';

@Crud({
    model: {
        type: EventEntity,
    },
    params: {
        id: {
            field: 'id',
            type: 'uuid',
            primary: true,
        },
        type: {
            field: 'type',
            enum: EventTypeEnum,
        },
    }
})
@Controller('event')
@ApiTags('event')
export class EventController implements CrudController<EventEntity> {
    constructor(public service: EventService) {}

    get base(): CrudController<EventEntity> {
        return this;
    }
}
