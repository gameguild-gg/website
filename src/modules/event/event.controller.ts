import { Controller } from '@nestjs/common';
import { EventService } from './event.service';
import { Crud, CrudController } from '@dataui/crud';
import { EventEntity } from './event.entity';
import { ApiTags } from '@nestjs/swagger';
import { EEventType } from '../../constants/event.enum';
import { ProposalCategory } from '../../constants/proposal.enum';

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
            enum: EEventType,
        },
        category: {
            field: 'category',
            enum: ProposalCategory,
        }
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
