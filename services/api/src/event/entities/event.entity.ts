import { Entity } from 'typeorm';
import { ContentBase } from "../../common/entities/content.base";

@Entity({ name: 'proposal' })
export class EventEntity extends ContentBase {
}
