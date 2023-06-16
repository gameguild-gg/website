// import { Injectable } from '@nestjs/common';
// import { JwtService } from '@nestjs/jwt';
//
// import { validateHash } from '../../common/utils';
// import type { UserRoleEnum } from '../user/user-role.enum';
// import { TokenType } from '../../constants';
// import { UserNotFoundException } from '../../exceptions';
// import { ApiConfigService } from '../../shared/services/api-config.service';
// import type { UserEntity } from '../user/user.entity';
// import { UserService } from '../user/user.service';
// import { TokenPayloadDto } from './dto/TokenPayloadDto';
// import type { UserLoginDto } from './dto/UserLoginDto';
//
// @Injectable()
// export class AuthService {
//     constructor(
//         private jwtService: JwtService,
//         private configService: ApiConfigService,
//         private userService: UserService,
//     ) {}
//
//     async emailExists(email: string): Promise<boolean> {
//         return !!(await this.userService.findOne({
//             where: {
//                 email,
//             },
//         }));
//     }
//
//     async registerEmailPass(email: string, password: string): Promise<TokenPayloadDto> {
//         if(await this.emailExists(email)) {
//             throw new Error('Email already exists');
//         }
//     }
//
//     async createAccessToken(data: {
//         role: UserRoleEnum;
//         userId: string;
//     }): Promise<TokenPayloadDto> {
//         return new TokenPayloadDto({
//             expiresIn: this.configService.authConfig.jwtExpirationTime,
//             accessToken: await this.jwtService.signAsync({
//                 userId: data.userId,
//                 type: TokenType.ACCESS_TOKEN,
//                 role: data.role,
//             }),
//         });
//     }
//
//     async validateUser(userLogin: Partial<UserEntity>): Promise<UserEntity> {
//
//         const user = await this.userService.findOne({
//             where: {
//                 email: userLogin.email,
//             }
//         });
//
//         const isPasswordValid = await validateHash(
//             userLogin.password,
//             user?.password,
//         );
//
//         if (!isPasswordValid) {
//             throw new UserNotFoundException();
//         }
//
//         return user!;
//     }
// }