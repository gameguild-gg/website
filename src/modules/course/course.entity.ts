import { EntityBase } from "../../common/entity.base";
import { Entity } from "typeorm";

@Entity({ name: 'course' })
export class CourseEntity extends EntityBase {}
