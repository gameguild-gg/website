import { Entity } from 'typeorm';
import { ObjectType } from '@nestjs/graphql';
import { ContentBase } from '../../cms/entities/content.base';

@ObjectType()
@Entity({ name: 'proposal' })
export class EventEntity extends ContentBase {}
