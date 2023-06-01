import {Column, Entity, Index, OneToMany} from "typeorm";
import {EntityBase} from "../../common/entity.base";
import {ProposalTypeEnum} from "./proposal.enum";
import {VoteEntity} from "../vote/vote.entity";
import {ApiProperty} from "@nestjs/swagger";
import {IsEnum, IsOptional} from "class-validator";
import { EventTypeEnum } from "../event/event.enum";

@Entity({ name: 'proposal' })
export class ProposalEntity extends EntityBase {
    @Column({default: 'untitled', type: "text", nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true})
    title: string;

    @Column({default: 'untitled', type: "text", nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true})
    slug: string;

    @Column({default: 'untitled', type: "text", nullable: false })
    @ApiProperty({required: true})
    description: string;

    @Column({enum: EventTypeEnum, nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true, enum: EventTypeEnum})
    @IsEnum(EventTypeEnum, {message: 'type must be one of the following values: ' + Object.values(EventTypeEnum).join(', ')} )
    type: EventTypeEnum;

    @Column({enum: ProposalTypeEnum, nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true, enum: ProposalTypeEnum})
    @IsEnum(ProposalTypeEnum)
    category: ProposalTypeEnum;

    @OneToMany(type => VoteEntity, vote => vote.proposal)
    @IsOptional()
    votes: VoteEntity[];
}