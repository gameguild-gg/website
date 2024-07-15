import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { AssetEntity } from './asset.entity';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { ApiConfigService } from '../common/config.service';

@Injectable()
export class AssetService extends TypeOrmCrudService<AssetEntity> {
  private readonly logger = new Logger(AssetService.name);

  constructor(
    @InjectRepository(AssetEntity)
    private readonly assetRepository: Repository<AssetEntity>,
    private readonly configService: ApiConfigService,
  ) {
    super(assetRepository);
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
}
