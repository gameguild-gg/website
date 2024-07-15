import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { AssetEntity } from './asset.entity';
import { AssetService } from './asset.service';
import { AssetController } from './asset.controller';

@Module({
  imports: [TypeOrmModule.forFeature([AssetEntity])],
  controllers: [AssetController],
  providers: [AssetService],
  exports: [AssetService],
})
export class ContentModule {}
