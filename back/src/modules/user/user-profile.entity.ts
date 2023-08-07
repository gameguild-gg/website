import { EntityBase } from '../../common/entity.base';
import {Column, Entity, OneToMany, OneToOne} from 'typeorm';
import { EventEntity } from '../event/event.entity';
import {ApiProperty} from "@nestjs/swagger";
import {UserEntity} from "./user.entity";

@Entity({ name: 'user-profile' })
export class UserProfileEntity extends EntityBase {
    @Column()
    @ApiProperty()
    fullName: string;
    
    @Column()
    @ApiProperty()
    cpf: string;

    // relations
    @OneToOne(() => UserEntity, (user) => user.profile) // specify inverse side as a second parameter
    user: UserEntity;

    @OneToMany(() => EventEntity, (event) => event.id)
    events: EventEntity;
}
