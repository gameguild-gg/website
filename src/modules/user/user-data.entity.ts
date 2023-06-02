import { EntityBase } from '../../common/entity.base';
import { Column, Entity, OneToMany } from 'typeorm';
import { EventEntity } from '../event/event.entity';
import { UserRoleEnum } from './user-role.enum';

@Entity({ name: 'user-data' })
export class UserDataEntity extends EntityBase {
    @Column()
    username: string;
    
    @Column()
    firstname: string;
    
    @Column()
    lastname: string;
    
    @Column()
    email: string;
    
    @Column()
    cpf: string;
    
    @Column({ enum: UserRoleEnum})
    role: UserRoleEnum;
    
    @Column()
    active: boolean;

    // relations
    @OneToMany(() => EventEntity, (event) => event.id)
    events: EventEntity;
    
    // @OneToMany(() => CourseEntity, (course) => course.id)
    // courses: CourseEntity;
}
