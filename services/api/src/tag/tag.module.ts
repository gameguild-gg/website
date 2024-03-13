import { Module } from '@nestjs/common';
import { TypeOrmModule } from "@nestjs/typeorm";
import { TagEntity } from "./tag.entity";
import { TagService } from './tag.service';

@Module({
  imports: [TypeOrmModule.forFeature([TagEntity])],
  providers: [TagService],
  exports: [TagService],
})
export class TagModule {}
