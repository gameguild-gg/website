import {Entity, ManyToOne} from "typeorm";
import {EntityBase} from "../../common/entity.base";
import {ProposalEntity} from "../proposal/proposal.entity";
import {ApiProperty} from "@nestjs/swagger";

@Entity({ name: 'vote' })
export class VoteEntity extends EntityBase {
    @ManyToOne(type => ProposalEntity, proposal => proposal.votes)
    // @ApiProperty({ type: () => ProposalEntity })
    proposal: ProposalEntity;
}