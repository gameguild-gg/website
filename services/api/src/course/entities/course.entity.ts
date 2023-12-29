import { Entity } from 'typeorm';
import { ContentBase } from '../../common/entities/content.base';

@Entity({ name: 'course' })
export class CourseEntity extends ContentBase {}
