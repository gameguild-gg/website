import {
  Body,
  Controller,
  Logger,
  Post,
  UploadedFile,
  UseInterceptors,
} from '@nestjs/common';
import {
  ApiBody,
  ApiConsumes,
  ApiProperty,
  ApiResponse,
  ApiTags,
} from '@nestjs/swagger';
import { AssetService } from './asset.service';
import { Auth, AuthUser } from '../auth';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { UserEntity } from '../user/entities';
import { FileInterceptor } from '@nestjs/platform-express';
import { ApiFile } from '../common/decorators/api-file.decorator';
import { CourseEntity } from '../cms/entities/course.entity';
import { ContentBase } from '../cms/entities/content.base';
import { ImageEntity } from './image.entity';

@Controller('asset')
@ApiTags('asset')
export class AssetController {
  private readonly logger = new Logger(AssetController.name);
  constructor(private readonly assetService: AssetService) {}

  // @Auth(AuthenticatedRoute)
  @UseInterceptors(FileInterceptor('file'))
  @ApiFile()
  @Post('upload')
  @Auth(AuthenticatedRoute)
  @ApiResponse({ type: ImageEntity })
  async uploadImage(
    @AuthUser() user: UserEntity,
    @Body() body: ContentBase,
    @UploadedFile() file: Express.Multer.File,
  ) {
    const ret = {
      filename: file.originalname,
      size: file.size,
      mimeType: file.mimetype,
    };

    console.log('ret', ret);
    return ret;
  }
}
