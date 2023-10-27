import { Entity, ManyToOne, Column, OneToMany } from "typeorm";
import { EntityBase } from '../../../common/entity.base';
import { UserEntity } from '../../user/user.entity';
import { CompetitionMatchEntity } from "./competition.match.entity";

@Entity()
export class CompetitionSubmissionEntity extends EntityBase {
  // relation with user
  @ManyToOne(() => UserEntity, (user) => user.competitionSubmissions)
  user: UserEntity;

  // zip file with the source code
  @Column({ type: 'bytea', nullable: false })
  sourceCodeZip: Uint8Array;

  @OneToMany(() => CompetitionMatchEntity, (m) => m.cat)
  matchesAsCat: CompetitionSubmissionEntity[];

  @OneToMany(() => CompetitionMatchEntity, (m) => m.catcher)
  matchesAsCatcher: CompetitionSubmissionEntity[];
}
