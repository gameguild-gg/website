import {Column, Entity, Index, OneToMany} from "typeorm";
import {EntityBase} from "../../common/entity.base";
import {ProposalCategory} from "../../constants/proposal.enum";
import {VoteEntity} from "../vote/vote.entity";
import {ApiProperty} from "@nestjs/swagger";
import {IsEnum, IsOptional} from "class-validator";
import { EEventType } from "../../constants/event.enum";

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

    @Column({enum: EEventType, type: "enum", nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true, enum: EEventType})
    @IsEnum(EEventType, {message: 'type must be one of the following values: ' + Object.values(EEventType).join(', ')} )
    type: EEventType;

    @Column({type: "enum", enum: ProposalCategory, nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true, enum: ProposalCategory})
    @IsEnum(ProposalCategory)
    category: ProposalCategory;

    @OneToMany(type => VoteEntity, vote => vote.proposal)
    @IsOptional()
    votes: VoteEntity[];
}