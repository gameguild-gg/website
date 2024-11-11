import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { AssetBase } from './asset.base';
import { AssetService } from './asset.service';
import { AssetController } from './asset.controller';
import { ImageEntity } from './image.entity';
import { S3ImageStorage } from './storages/s3.image.storage';
import { ApiConfigService } from '../common/config.service';
import { ConfigModule } from '@nestjs/config';

@Module({
  imports: [TypeOrmModule.forFeature([ImageEntity])],
  controllers: [AssetController],
  providers: [AssetService],
  exports: [AssetService],
})
export class AssetModule {}
