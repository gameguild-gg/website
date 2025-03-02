import { ContentModel } from '@/common/models/content.model';
import { UserProfileDto } from '@/user/modules/user-profile/dtos/user-profile.dto';
import { UserModel } from '@/user/model/user.model';

export class UserProfileModel extends ContentModel implements UserProfileDto {
  public readonly user: UserModel;

  public readonly givenName?: string;

  public readonly familyName?: string;

  public readonly displayName?: string;
}
