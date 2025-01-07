import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { AssetSourceType } from './asset.base';
import { Repository } from 'typeorm';
import { ApiConfigService, SourceInfo } from '../common/config.service';
import { ImageEntity } from './image.entity';
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

  public async findExternalImageURL(url: string): Promise<ImageEntity> {
    return this.imageRepository.findOne({
      where: { source: AssetSourceType.EXTERNAL, path: url },
    });
  }

  public async StoreImageFromURL(
    url: string,
    description: string = '',
  ): Promise<ImageEntity> {
    return this.imageRepository.save({
      source: AssetSourceType.EXTERNAL,
      path: url,
      description: description,
    });
  }

  public async storeImage(file: Express.Multer.File): Promise<ImageEntity> {
    // check if image already exists
    // todo: implement megaupload hashing system to avoid duplicates

    const asset = await this.imageStorage.store(file);
    const folder =
      asset.hash.substring(0, 2) + '/' + asset.hash.substring(2, 4);

    return this.imageRepository.save({
      width: asset.width,
      height: asset.height,
      mimetype: asset.mime,
      path: folder,
      source: AssetSourceType.S3,
      hash: asset.hash,
      filename: asset.hash + '-' + file.originalname,
      sizeBytes: asset.size,
      originalFilename: file.originalname,
    });
  }

  async deleteImage(picture: ImageEntity) {
    // todo: deal with different sources
    // parallel await
    await Promise.all([
      this.imageStorage.delete(picture),
      this.imageRepository.delete(picture.id),
    ]);
  }
}
