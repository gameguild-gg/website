import { Profile } from 'passport-google-oauth20';

export class GitHubSignInPayloadDto {
  public readonly accessToken: string;
  public readonly refreshToken: string;
  public readonly profile: Profile;
}
