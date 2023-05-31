import { EntityBase } from '../../common/entity.base';
import { Column, Entity, ManyToOne } from 'typeorm';
import { UserEntity } from '../user/user.entity';
import { EEventType } from '../../constants/event.enum';

@Entity({ name: 'event' })
export class EventEntity extends EntityBase {
    @Column()
    title: string;

    @Column()
    day: Date;

    @Column()
    time: Date;

    @Column()
    type: EEventType;

    // relations
    @ManyToOne(() => UserEntity, (user) => user.events)
    creator: UserEntity;
}
