import { Entity } from 'typeorm';
import { ContentBase } from "../../course/entities/content.base";

@Entity({ name: 'proposal' })
export class EventEntity extends ContentBase {}
