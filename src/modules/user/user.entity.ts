import { EntityBase } from '../../common/entity.base';
import {Column, Entity, JoinColumn, OneToMany, OneToOne} from 'typeorm';
import { UserRoleEnum } from './user-role.enum';
import {ApiProperty} from "@nestjs/swagger";
import {UserProfileEntity} from "./user-profile.entity";

@Entity({ name: 'user' })
export class UserEntity extends EntityBase {
    @Column()
    @ApiProperty()
    username: string;

    @Column({ enum: UserRoleEnum})
    @ApiProperty({enum: UserRoleEnum})
    role: UserRoleEnum;
    
    @Column()
    @ApiProperty()
    active: boolean;

    // relations
    @OneToOne(() => UserProfileEntity, (profile) => profile.user) // specify inverse side as a second parameter
    @JoinColumn()
    profile: UserProfileEntity
}
