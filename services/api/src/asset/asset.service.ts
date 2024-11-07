import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { AssetBase } from './asset.base';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { ApiConfigService, SourceInfo } from '../common/config.service';
import { ImageEntity } from './image.entity';

@Injectable()
export class AssetService {
  private readonly logger = new Logger(AssetService.name);
  private readonly tempFolder;
  private assetSourceMap: Map<string, SourceInfo>;

  constructor(
    // @InjectRepository(AssetBase)
    // private readonly imageRepository: Repository<AssetBase>,
    private readonly configService: ApiConfigService,
  ) {
    this.assetSourceMap = configService.assetSourceInfoMap;
  }

  private async store(file: Express.Multer.File): Promise<ImageEntity> {
    // save it to temp folder
    return new ImageEntity();
  }
}
