import { ModelBase } from '@/common/models/model.base';
import { UserProfileDto } from '@/user/modules/user-profile/dtos/user-profile.dto';

import { UserDto } from '../dtos/user.dto';

export class UserModel extends ModelBase implements UserDto {
  // Local Sign-in

  public readonly username: string;

  public readonly email: string;

  public readonly emailVerified: boolean;

  public readonly passwordHash: string;

  public readonly passwordSalt: string;

  // Social Sign-in

  public readonly appleId: string;

  public readonly facebookId: string;

  public readonly githubId: string;

  public readonly googleId: string;

  public readonly linkedinId: string;

  public readonly twitterId: string;

  // Web3 Sign-in

  public readonly walletAddress: string;

  // Profile
  public readonly profile: UserProfileDto;
}
