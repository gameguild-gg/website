import { EntityBase } from "../../common/entity.base";
import { Entity } from "typeorm";

@Entity({ name: 'chapter' })
export class ChapterEntity extends EntityBase {}
