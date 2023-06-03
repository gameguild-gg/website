import {Controller, Get} from "@nestjs/common";
import {ApiTags} from "@nestjs/swagger";
import {ProposalService} from "./proposal.service";
import {Crud, CrudController} from "@dataui/crud";
import {ProposalEntity} from "./proposal.entity";
import {ProposalTypeEnum} from "./proposal.enum";
import { EventTypeEnum } from "../event/event.enum";

@Crud({
    model: {
        type: ProposalEntity,
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
        category: {
            field: 'category',
            enum: ProposalTypeEnum,
        }
    }
})
@Controller('proposal')
@ApiTags('proposal')
export class ProposalController implements CrudController<ProposalEntity>{
    constructor(public service: ProposalService) {}

    get base(): CrudController<ProposalEntity> {
        return this;
    }
}