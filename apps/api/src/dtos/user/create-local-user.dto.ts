export class CreateLocalUserDto {
  email: string;
  passwordHash: string;
  passwordSalt: string;
  username?: string;

  constructor(partial: Partial<CreateLocalUserDto>) {
    Object.assign(this, partial);
  }
}
