import {Column, Entity, Index, OneToMany} from "typeorm";
import {EntityBase} from "../../common/entity.base";
import {ProposalCategory, ProposalType} from "./proposal.enum";
import {VoteEntity} from "../vote/vote.entity";

@Entity({ name: 'proposal' })
export class ProposalEntity extends EntityBase {
    @Column({default: 'untitled', type: "text", nullable: false })
    @Index({unique: false})
    title: string;

    @Column({default: 'untitled', type: "text", nullable: false })
    @Index({unique: false})
    slug: string;

    @Column({default: 'untitled', type: "text", nullable: false })
    description: string;

    @Column({enum: ProposalType, type: "enum", nullable: false })
    @Index({unique: false})
    type: ProposalType;

    @Column({type: "enum", enum: ProposalCategory, nullable: false })
    @Index({unique: false})
    category: ProposalCategory;

    @OneToMany(type => VoteEntity, vote => vote.proposal)
    votes: VoteEntity[];
}