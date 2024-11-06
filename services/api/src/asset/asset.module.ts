import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { AssetBase } from './asset.base';
import { AssetService } from './asset.service';
import { AssetController } from './asset.controller';

@Module({
  // imports: [TypeOrmModule.forFeature([AssetBase])],
  controllers: [AssetController],
  providers: [AssetService],
  exports: [AssetService],
})
export class AssetModule {}
