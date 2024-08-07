import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { AssetService } from './asset.service';

@Controller('asset')
@ApiTags('asset')
export class AssetController {
  private readonly logger = new Logger(AssetController.name);
  constructor(private readonly assetService: AssetService) {}
}
