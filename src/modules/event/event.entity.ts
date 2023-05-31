import { EntityBase } from "../../common/entity.base";
import { Entity } from "typeorm";

@Entity({ name: 'event' })
export class EventEntity extends EntityBase {}
