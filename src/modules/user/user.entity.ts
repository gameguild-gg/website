import { EntityBase } from "../../common/entity.base";
import { Entity } from "typeorm";

@Entity({ name: 'user' })
export class UserEntity extends EntityBase {}
