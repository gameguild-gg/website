import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { UserProfileEntity } from './entities/user-profile.entity';
import { UserEntity } from 'src/user/entities';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserProfilePatchDto } from './dtos/user-profile-patch.dto';
import { error } from 'console';

@Injectable()
export class UserProfileService {
  private readonly logger = new Logger(UserProfileService.name);

  constructor(
    @InjectRepository(UserProfileEntity)
    private readonly userProfileRepository: Repository<UserProfileEntity>,
    @InjectRepository(UserEntity)
    private readonly userRepository: Repository<UserEntity>,
  ) {}
  // create
  async save(profile: Partial<UserProfileEntity>): Promise<UserProfileEntity> {
    return this.userProfileRepository.save(profile);
  }

  async getMe(userId: string): Promise<UserProfileEntity>{
    const user = await this.userRepository.findOne({
      relations: ['profile'],
      where: {id: userId},
    })
    return user.profile
  }

  async patchMe(userId: string, profileDto: UserProfilePatchDto): Promise<UserProfileEntity>{
    console.log("service. profileDto: ",profileDto)
    const myProfile = await this.getMe(userId)
    
    const response = await this.userProfileRepository.update(
      {id: myProfile.id},
      {
        picture: profileDto.picture,
        name: profileDto.name,
        bio: profileDto.bio,
      }
    )
    if (response.affected>0){
      return myProfile
    } else {
      throw new NotFoundException('Profile not found');
    }
    //return this.userProfileRepository.save(profile)
  }

}
