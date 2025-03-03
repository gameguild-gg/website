import { Profile } from 'passport-google-oauth20';

export class GoogleSignInPayloadDto {
  public readonly accessToken: string;
  public readonly refreshToken: string;
  public readonly profile: Profile;
}
