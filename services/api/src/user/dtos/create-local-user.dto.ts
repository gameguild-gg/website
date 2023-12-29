export class CreateLocalUserDto {
  email: string;
  passwordHash: string;
  passwordSalt: string;
  username?: string;
}
