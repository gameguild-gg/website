import {EntityBase} from '../../../common/entity.base';
import {OneToMany} from 'typeorm';
import {CompetitionMatchEntity} from './competition.match.entity';

export class CompetitionRunEntity extends EntityBase {
    @OneToMany(
        () => CompetitionMatchEntity,
        (competitionMatch) => competitionMatch.competitionRun,
    )
    competitionMatches: CompetitionMatchEntity[];
}
