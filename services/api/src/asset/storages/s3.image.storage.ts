import { OkDto } from 'src/common/dtos/ok.dto';
import { AssetBase } from '../asset.base';
import { Storage } from '../storage';
import {
  GetObjectCommand,
  PutObjectCommand,
  S3Client,
} from '@aws-sdk/client-s3';
import { ApiConfigService } from '../../common/config.service';
import {
  AssetOnDisk,
  AssetOnDiskWithWidthAndHeight,
  FileCacheStorageService,
} from './filecache.storage';
import { Inject, Injectable, Logger } from '@nestjs/common';
import * as fs from 'node:fs';

const sharp = require('sharp');

export type S3StorageConfig = {
  endpoint: string;
  accessKey: string;
  secretKey: string;
  bucket: string;
  port: number;
};

@Injectable()
export class S3ImageStorage extends Storage {
  client: S3Client;
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
    this.config = sourceInfo['MINIO'];

    // init s3 client
    this.client = new S3Client({
      region: 'us-east-1',
      credentials: {
        accessKeyId: this.config.accessKey,
        secretAccessKey: this.config.secretKey,
      },
      endpoint: this.config.endpoint,
      forcePathStyle: true,
      tls: this.config.endpoint.startsWith('https'),
    });
    this.assetFolder = configService.assetCacheDir;
  }

  async store(
    file: Express.Multer.File,
  ): Promise<AssetOnDiskWithWidthAndHeight> {
    const metadata = await sharp(file.path).metadata();
    const cached = await this.filecache.store(file);

    const fileStream = fs.createReadStream(cached.path);
    const path = `${cached.hash.substring(0, 2)}/${cached.hash.substring(2, 4)}`;

    const command = new PutObjectCommand({
      Bucket: this.config.bucket,
      Key: `${path}/${cached.hash}-${file.originalname}`,
      Body: fileStream,
      ContentType: cached.mime,
      Metadata: {
        Size: cached.size.toString(),
        Hash: cached.hash,
        Width: metadata.width.toString(),
        Height: metadata.height.toString(),
      },
    });

    await this.client.send(command);

    fileStream.close();

    return { ...cached, width: metadata.width, height: metadata.height };
  }
  delete(): Promise<OkDto> {
    throw new Error('Method not implemented.');
  }
  async get(asset: Partial<AssetBase>): Promise<AssetOnDisk> {
    // fetch from filecache first, then from s3
    const fileOnDisk = await this.filecache.get(asset);
    if (fileOnDisk) return fileOnDisk;

    // fetch from s3 and store in filecache
    const command = new GetObjectCommand({
      Bucket: this.config.bucket,
      Key: `${asset.path}`,
    });

    // fetch the asset from s3
    const assetOnS3 = await this.client.send(command);

    // save the file to disk
    const targetFolder = `${this.assetFolder}/${asset.hash.substring(0, 2)}/${asset.hash.substring(2, 4)}`;
    const targetPath = `${targetFolder}/${asset.hash}-${asset.filename}`;
    const stream = assetOnS3.Body.transformToWebStream();
    await this.filecache.saveToDisk(stream, targetPath);
    await stream.cancel();
    return {
      path: targetPath,
      hash: asset.hash,
      size: asset.sizeBytes,
      mime: asset.mimetype,
    };
  }
}
