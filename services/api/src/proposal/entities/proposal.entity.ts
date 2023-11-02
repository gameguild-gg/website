import { Entity } from 'typeorm';
import { ContentBase } from '../../common/entities/content.base';

@Entity({ name: 'proposal' })
export class ProposalEntity extends ContentBase {
  // @OneToMany((type) => VoteEntity, (vote) => vote.proposal)
  // votes: VoteEntity[];
}
