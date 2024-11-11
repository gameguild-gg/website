import { OkDto } from 'src/common/dtos/ok.dto';
import { AssetBase } from '../asset.base';
import { Storage } from '../storage';
import * as Minio from 'minio';
import { ImageEntity } from '../image.entity';
import { ApiConfigService } from '../../common/config.service';
import { FileCacheStorageService } from './filecache.storage';
import * as gm from 'gm';
import { Inject, Logger } from '@nestjs/common';

export type S3StorageConfig = {
  endpoint: string;
  accessKeyId: string;
  secretAccessKey: string;
  bucket: string;
  port: number;
};

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
    if (!sourceInfo || !sourceInfo.has('MINIO')) {
      const msg = 'Minio connection info not found. Check your env variables.';
      this.logger.error(msg);
      throw new Error(msg);
    }
    const minio = sourceInfo.get('MINIO');

    // init s3 client
    this.client = new Minio.Client({
      endPoint: minio.endpoint,
      port: minio.port,
      accessKey: minio.accessKey,
      secretKey: minio.secretKey,
    });
    this.assetFolder = configService.assetCacheDir;
  }

  checkIfGmIsInstalled() {
    gm('version', (err, version) => {
      if (err) {
        const message = 'GraphicsMagick is not installed. Please install it.';
        this.logger.error(message);
        throw new Error(message);
      } else {
        this.logger.verbose(
          'GraphicsMagick is installed with with version: ' + version,
        );
      }
    });
  }

  async store(file: Express.Multer.File) {
    const cached = await this.filecache.store(file);

    // upload the file to minio
    await this.client.fPutObject(
      this.config.bucket,
      cached.hash,
      `${cached.hash.substring(0, 2)}/${cached.hash.substring(2, 4)}/${cached.hash}-${file.originalname}`,
      {
        'Content-Type': file.mimetype,
        Size: file.size,
        Hash: cached.hash,
      },
    );

    // get the dimensions of the image
    const dimensions = await new Promise<{ width: number; height: number }>(
      (resolve, reject) => {
        gm(file.path).size((err, size) => {
          if (err) {
            reject(err);
          }
          resolve(size);
        });
      },
    );

    return {};
  }
  delete(): Promise<OkDto> {
    throw new Error('Method not implemented.');
  }
  get(asset: Partial<AssetBase>): Promise<AssetBase> {
    throw new Error('Method not implemented.');
  }
}
