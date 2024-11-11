import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { AssetBase } from './asset.base';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { ApiConfigService, SourceInfo } from '../common/config.service';
import { ImageEntity } from './image.entity';
import * as fs from 'node:fs';
import * as path from 'path';

@Injectable()
export class AssetService {
  private readonly logger = new Logger(AssetService.name);
  private readonly tempFolder;
  private assetSourceMap: Map<string, SourceInfo>;
  private assetCacheDir: string;
  private ttl: number;
  private maxCacheSize: number;

  constructor(
    // @InjectRepository(AssetBase)
    // private readonly imageRepository: Repository<AssetBase>,
    private readonly configService: ApiConfigService,
  ) {
    this.assetSourceMap = configService.assetSourceInfoMap;
    this.assetCacheDir = configService.assetCacheDir;
    this.maxCacheSize = configService.assetCacheSize;
    this.ttl = configService.assetCacheTTL;
  }

  private async store(file: Express.Multer.File): Promise<ImageEntity> {
    // save the file to the temp/uploads folder without any processing
    const nowms = new Date().getTime();
    // const tempFilePath = path.join(this.assetCacheDir, file.originalname);

    const path = `${this.tempFolder}/${nowms}${file.originalname}`;

    // save it to temp folder
    return new ImageEntity();
  }
}
