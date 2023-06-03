import {TypeOrmModule} from "@nestjs/typeorm";
import {ProposalEntity} from "./proposal.entity";
import {ProposalService} from "./proposal.service";
import {ProposalController} from "./proposal.controller";
import {Module} from "@nestjs/common";

@Module({
    imports: [TypeOrmModule.forFeature([ProposalEntity])],
    controllers: [ProposalController],
    exports: [ProposalService],
    providers: [ProposalService],
})
export class ProposalModule {}