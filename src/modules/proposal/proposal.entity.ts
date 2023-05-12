import {Column, Entity, Index, OneToMany} from "typeorm";
import {EntityBase} from "../../common/entity.base";
import {ProposalCategory, ProposalType} from "./proposal.enum";
import {VoteEntity} from "../vote/vote.entity";
import {ApiProperty} from "@nestjs/swagger";
import {IsEnum, IsOptional} from "class-validator";

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

    @Column({enum: ProposalType, type: "enum", nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true, enum: ProposalType})
    @IsEnum(ProposalType, {message: 'type must be one of the following values: ' + Object.values(ProposalType).join(', ')} )
    type: ProposalType;

    @Column({type: "enum", enum: ProposalCategory, nullable: false })
    @Index({unique: false})
    @ApiProperty({required: true, enum: ProposalCategory})
    @IsEnum(ProposalCategory)
    category: ProposalCategory;

    @OneToMany(type => VoteEntity, vote => vote.proposal)
    @IsOptional()
    votes: VoteEntity[];
}