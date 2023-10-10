import { Entity } from 'typeorm';
import { EntityBase } from '../../common/entities/entity.base';

@Entity({ name: 'vote' })
export class VoteEntity extends EntityBase {
  // @ManyToOne((type) => ProposalEntity, (proposal) => proposal.votes)
  // proposal: ProposalEntity;
}