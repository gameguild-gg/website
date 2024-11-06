import { OkDto } from 'src/common/dtos/ok.dto';
import { AssetBase } from './asset.base';
import { Storage } from './storage';

// s3 client
import { S3Client, ListBucketsCommand } from '@aws-sdk/client-s3';

export type S3StorageConfig = {
  endpoint: string;
  accessKeyId: string;
  secretAccessKey: string;
  bucket: string;
};

export class S3Storage implements Storage {
  client: S3Client;

  constructor(private config: S3StorageConfig) {
    // init s3 client
    this.client = new S3Client({
      endpoint: this.config.endpoint,
      credentials: {
        accessKeyId: this.config.accessKeyId,
        secretAccessKey: this.config.secretAccessKey,
      },
    });
  }

  store(file: Express.Multer.File): Promise<AssetBase> {
    throw new Error('Method not implemented.');
  }
  delete(): Promise<OkDto> {
    throw new Error('Method not implemented.');
  }
  get(asset: Partial<AssetBase>): Promise<AssetBase> {
    throw new Error('Method not implemented.');
  }
}
