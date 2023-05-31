import { EntityBase } from '../../common/entity.base';
import { Column, Entity, OneToMany } from 'typeorm';
import { EventEntity } from '../event/event.entity';
import { ERole } from '../../constants/role.enum';
import { CourseEntity } from '../course/course.entity';

@Entity({ name: 'user' })
export class UserEntity extends EntityBase {
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
    
    @Column()
    role: ERole;
    
    @Column()
    active: boolean;

    // relations
    @OneToMany(() => EventEntity, (event) => event.id)
    events: EventEntity;
    
    // @OneToMany(() => CourseEntity, (course) => course.id)
    // courses: CourseEntity;
}
