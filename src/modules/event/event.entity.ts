import { EntityBase } from '../../common/entity.base';
import { Column, Entity, ManyToOne } from 'typeorm';

import { EventTypeEnum } from './event.enum';
import {UserDataEntity} from "../user/user-data.entity";

@Entity({ name: 'event' })
export class EventEntity extends EntityBase {
    @Column()
    title: string;

    @Column()
    day: Date;

    @Column()
    time: Date;

    @Column()
    type: EventTypeEnum;

    // relations
    @ManyToOne(() => UserDataEntity, (user) => user.events)
    creator: UserDataEntity;
}
