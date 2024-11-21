import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { AssetBase } from './asset.base';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { ApiConfigService, SourceInfo } from '../common/config.service';
import { ImageEntity } from './image.entity';
import * as fs from 'node:fs';
import * as path from 'path';
import { S3ImageStorage } from './storages/s3.image.storage';

@Injectable()
export class AssetService {
  private readonly logger = new Logger(AssetService.name);
  private readonly tempFolder;
  private assetSourceMap: Map<string, SourceInfo>;
  private assetCacheDir: string;
  private ttl: number;
  private maxCacheSize: number;

  constructor(
    @InjectRepository(ImageEntity)
    private readonly imageRepository: Repository<ImageEntity>,
    private readonly configService: ApiConfigService,
    private imageStorage: S3ImageStorage,
  ) {
    this.assetSourceMap = configService.assetSourceInfoMap;
    this.assetCacheDir = configService.assetCacheDir;
    this.maxCacheSize = configService.assetCacheSize;
    this.ttl = configService.assetCacheTTL;
  }

  public async storeImage(file: Express.Multer.File): Promise<ImageEntity> {
    // check if image already exists
    // todo: implement megaupload hashing system to avoid duplicates

    const assetOnDisk = await this.imageStorage.store(file);
    const folder =
      assetOnDisk.hash.substring(0, 2) + '/' + assetOnDisk.hash.substring(2, 4);

    const image = await this.imageRepository.save({
      width: assetOnDisk.width,
      height: assetOnDisk.height,
      mimetype: assetOnDisk.mime,
      path: folder,
    });

    return image;
  }
}
