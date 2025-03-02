// import { Injectable, Logger } from '@nestjs/common';
// import { PassportStrategy } from '@nestjs/passport';
// import { Strategy } from 'passport-google-oauth20';
// import { CommandBus } from '@nestjs/cqrs';
// import { ValidateSignInWithGoogleCommand } from '@/auth/commands/validate-sign-in-with-google.command';
// import { UserDto } from '@/user/dtos/user.dto';
//
// @Injectable()
// export class GoogleStrategy extends PassportStrategy(Strategy, 'google') {
//   private readonly logger = new Logger(GoogleStrategy.name);
//
//   constructor(
//     private readonly commandBus: CommandBus,
//     // private readonly queryBus: QueryBus,
//   ) {
//     super({});
//   }
//
//   public async validate(idToken: string): Promise<UserDto> {
//     this.commandBus.execute(new ValidateSignInWithGoogleCommand(idToken));
//   }
// }
