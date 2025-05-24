import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserProfileEntity } from './entities/user-profile.entity';
import { UserEntity } from '../../entities';
import { AssetService } from '../../../asset';

@Injectable()
export class UserProfileService {
  private readonly logger = new Logger(UserProfileService.name);

  constructor(
    @InjectRepository(UserProfileEntity)
    public readonly repository: Repository<UserProfileEntity>,
    public readonly assetService: AssetService,
  ) {}

  async updateProfilePicture(user: UserEntity, file: Express.Multer.File): Promise<UserProfileEntity> {
    const profile = await this.repository.findOne({
      where: { user: { id: user.id } },
    });
    if (!profile) {
      throw new NotFoundException('User profile not found');
    }

    // if there is a picture, delete it
    if (profile.picture) await this.assetService.deleteImage(profile.picture);

    const img = await this.assetService.storeImage(file);

    // store and update profile picture
    profile.picture = img;
    await this.repository.update({ id: profile.id }, { picture: img });
    return profile;
  }
}
