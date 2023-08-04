import { EntityBase } from "../../common/entity.base";
import { Entity } from "typeorm";

@Entity({ name: 'post' })
export class PostEntity extends EntityBase {}
