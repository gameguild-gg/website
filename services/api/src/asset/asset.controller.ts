import {
  Body,
  Controller,
  Logger,
  Post,
  UnprocessableEntityException,
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
import { AuthenticatedRoute, PublicRoute } from '../auth/auth.enum';
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

  @ApiFile({
    fileFilter(
      req: any,
      file: Express.Multer.File,
      callback: (error: Error | null, acceptFile: boolean) => void,
    ) {
      if (!file.mimetype.startsWith('image')) {
        return callback(
          new UnprocessableEntityException('Only images are allowed!'),
          false,
        );
      }
      return callback(null, true);
    },
  })
  @Post('upload')
  // @Auth(AuthenticatedRoute)
  @ApiResponse({ type: ImageEntity })
  async uploadImage(
    @AuthUser() user: UserEntity,
    @UploadedFile() file: Express.Multer.File,
  ) {
    return this.assetService.storeImage(file);
  }
}
