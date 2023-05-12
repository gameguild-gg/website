import {Controller, Get} from "@nestjs/common";
import {ApiTags} from "@nestjs/swagger";
import {ProposalService} from "./proposal.service";

@Controller('proposal')
@ApiTags('proposal')
export class ProposalController {
    constructor(private proposalService: ProposalService) {}

    @Get('/hello')
    hello(): string {
        return "Hello World!";
    }
}