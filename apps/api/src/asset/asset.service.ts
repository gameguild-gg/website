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
      hash: this.generateHashFromURL(url),
      filename: this.extractFilenameFromURL(url),
      originalFilename: this.extractFilenameFromURL(url),
      mimetype: 'image/jpeg', // Default mimetype, could be improved with content detection
      sizeBytes: 0, // Default size, could be improved with actual size detection
    });
  }

  /**
   * Generates a hash from a URL for external images
   * @param url The URL to generate a hash from
   * @returns A hash string
   */
  private generateHashFromURL(url: string): string {
    // Simple hash function for URLs
    let hash = 0;
    for (let i = 0; i < url.length; i++) {
      const char = url.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash; // Convert to 32bit integer
    }
    // Convert to hex string and ensure it's positive
    return Math.abs(hash).toString(16);
  }

  /**
   * Extracts a filename from a URL
   * @param url The URL to extract a filename from
   * @returns The extracted filename or a default name
   */
  private extractFilenameFromURL(url: string): string {
    try {
      const urlObj = new URL(url);
      const pathname = urlObj.pathname;
      const filename = pathname.split('/').pop() || 'external-image';
      return filename.length > 0 ? filename : 'external-image';
    } catch (e) {
      return 'external-image';
    }
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
