import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { AssetBase } from './asset.base';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { ApiConfigService } from '../common/config.service';
import { ImageEntity } from './image.entity';

@Injectable()
export class AssetService {
  private readonly logger = new Logger(AssetService.name);
  private readonly tempFolder;

  constructor(
    // @InjectRepository(AssetBase)
    // private readonly imageRepository: Repository<AssetBase>,
    private readonly configService: ApiConfigService,
  ) {
    // super(assetRepository);
    this.mountS3();
  }

  private async mountS3() {
    try {
      const s3 = this.configService.s3Config;
    } catch (error) {
      this.logger.error('Error mounting S3', error);
      throw new Error(
        'Error mounting S3, did you forget to set the S3 env variables?',
      );
    }

    // mount-s3 DOC-EXAMPLE-BUCKET /path/to/mount
  }

  private async store(file: Express.Multer.File): Promise<ImageEntity> {
    // save it to temp folder
    return new ImageEntity();
  }
}
