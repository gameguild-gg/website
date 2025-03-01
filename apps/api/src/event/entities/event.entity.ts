import { Entity } from 'typeorm';
import { ContentBase } from "../../cms/entities/content.base";

@Entity({ name: 'proposal' })
export class EventEntity extends ContentBase {}
