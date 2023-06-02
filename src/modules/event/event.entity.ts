import { EntityBase } from '../../common/entity.base';
import { Column, Entity, ManyToOne } from 'typeorm';

import { EventTypeEnum } from './event.enum';
import {UserProfileEntity} from "../user/user-profile.entity";

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
    @ManyToOne(() => UserProfileEntity, (user) => user.events)
    creator: UserProfileEntity;
}
