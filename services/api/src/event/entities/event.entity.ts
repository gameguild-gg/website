import { Entity } from 'typeorm';
import { ContentBase } from "../../cms/entities/content.base";

@Entity({ name: 'event' })
export abstract class EventEntity extends ContentBase {}
