import { OkDto } from 'src/common/dtos/ok.dto';
import { AssetBase } from '../asset.base';
import { Storage } from '../storage';
import * as Minio from 'minio';
import { ImageEntity } from '../image.entity';
import { ApiConfigService } from '../../common/config.service';

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

  constructor(
    private config: S3StorageConfig,
    private configService: ApiConfigService,
  ) {
    super();
    // init s3 client
    this.client = new Minio.Client({
      endPoint: config.endpoint,
      port: config.port,
      accessKey: config.accessKeyId,
      secretKey: config.secretAccessKey,
    });
    this.assetFolder = configService.assetCacheDir;
  }

  store(file: Express.Multer.File) {
    // store the file in a temp folder
  }
  delete(): Promise<OkDto> {
    throw new Error('Method not implemented.');
  }
  get(asset: Partial<AssetBase>): Promise<AssetBase> {
    throw new Error('Method not implemented.');
  }
}
