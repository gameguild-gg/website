import { EntityDto } from "../../../../../packages/dtos/src/entity.dto";
import { ApiProperty } from "@nestjs/swagger";
import { Exclude } from "class-transformer";

export class UserDto extends EntityDto {
  // Local Sign-in
  @ApiProperty()
  username: string;
  
  @ApiProperty()
  email: string;
  
  @ApiProperty()
  emailVerified: boolean;
  
  @ApiProperty()
  @Exclude()
  passwordHash: string;
  
  @ApiProperty()
  @Exclude()
  passwordSalt: string;

  // Social Sign-in
  @ApiProperty()
  facebookId: string;
  
  @ApiProperty()
  googleId: string;
  
  @ApiProperty()
  githubId: string;
  
  @ApiProperty()
  appleId: string;
  
  @ApiProperty()
  linkedinId: string;
  
  @ApiProperty()
  twitterId: string;

  // Web3 Sign-in
  @ApiProperty()
  walletAddress: string;

  // chess elo rank
  @ApiProperty()
  elo: number;
}
