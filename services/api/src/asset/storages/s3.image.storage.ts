import { OkDto } from 'src/common/dtos/ok.dto';
import { AssetBase } from '../asset.base';
import { Storage } from '../storage';
import * as Minio from 'minio';
import { ImageEntity } from '../image.entity';
import { ApiConfigService } from '../../common/config.service';
import {
  AssetOnDisk,
  AssetOnDiskWithWidthAndHeight,
  FileCacheStorageService,
} from './filecache.storage';
import { Inject, Injectable, Logger } from '@nestjs/common';
const sharp = require('sharp');
import { lookup } from 'mime-types';

export type S3StorageConfig = {
  endpoint: string;
  accessKeyId: string;
  secretAccessKey: string;
  bucket: string;
  port: number;
};

@Injectable()
export class S3ImageStorage extends Storage {
  client: Minio.Client;
  assetFolder: string;
  private readonly logger = new Logger(S3ImageStorage.name);
  private config: S3StorageConfig;

  constructor(
    @Inject(ApiConfigService) private configService: ApiConfigService,
    private filecache: FileCacheStorageService,
  ) {
    super();
    // bug: configservice is arriving as undefined
    const sourceInfo = this.configService.assetSourceInfoMap;
    if (!sourceInfo || !sourceInfo['MINIO']) {
      const msg = 'Minio connection info not found. Check your env variables.';
      this.logger.error(msg);
      throw new Error(msg);
    }
    const minio = sourceInfo['MINIO'];

    // init s3 client
    this.client = new Minio.Client({
      endPoint: minio.endpoint,
      port: minio.port,
      accessKey: minio.accessKey,
      secretKey: minio.secretKey,
    });
    this.assetFolder = configService.assetCacheDir;
  }

  async store(
    file: Express.Multer.File,
  ): Promise<AssetOnDiskWithWidthAndHeight> {
    const metadata = await sharp(file.path).metadata();
    const cached = await this.filecache.store(file);

    // upload the file to minio
    await this.client.fPutObject(
      this.config.bucket,
      cached.hash,
      `${cached.hash.substring(0, 2)}/${cached.hash.substring(2, 4)}/${cached.hash}-${file.originalname}`,
      {
        'Content-Type': file.mimetype,
        Size: cached.size,
        Hash: cached.hash,
        Width: metadata.width,
        Height: metadata.height,
      },
    );

    return { ...cached, width: metadata.width, height: metadata.height };
  }
  delete(): Promise<OkDto> {
    throw new Error('Method not implemented.');
  }
  async get(asset: Partial<AssetBase>): Promise<AssetOnDisk> {
    // fetch from filecache first, then from s3
    const fileOnDisk = await this.filecache.get(asset);
    if (fileOnDisk) return fileOnDisk;
    // fetch from s3
    const assetOnS3 = await this.client.getObject(
      this.config.bucket,
      `${asset.hash.substring(0, 2)}/${asset.hash.substring(2, 4)}/${asset.hash}-${asset.filename}`,
    );

    // save the file to disk
    const targetFolder = `${this.assetFolder}/${asset.hash.substring(0, 2)}/${asset.hash.substring(2, 4)}`;
    const targetPath = `${targetFolder}/${asset.hash}-${asset.filename}`;
    await this.filecache.saveToDisk(assetOnS3, targetPath);
    return {
      path: targetPath,
      hash: asset.hash,
      size: asset.sizeBytes,
      mime: asset.mimetype,
    };
  }
}
