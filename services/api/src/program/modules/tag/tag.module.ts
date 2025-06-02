import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { Tag, TagRelationship, TagProficiency, CertificateTag } from '../../entities';

import { TagController } from './tag.controller';
import { TagService } from './tag.service';
import { TagRelationshipService } from './tag-relationship.service';

@Module({
  imports: [TypeOrmModule.forFeature([Tag, TagRelationship, TagProficiency, CertificateTag])],
  controllers: [TagController],
  providers: [TagService, TagRelationshipService],
  exports: [TagService, TagRelationshipService],
})
export class TagModule {}
