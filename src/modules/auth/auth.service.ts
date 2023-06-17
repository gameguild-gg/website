import {Injectable, NotImplementedException} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';

import {generateHash, generateRandomSalt, validateHash} from '../../common/utils';
import type { UserRoleEnum } from '../user/user-role.enum';
import type { UserEntity } from '../user/user.entity';
import { UserService } from '../user/user.service';
import type { UserLoginDto } from './user-login.dto';
import {TokenType} from "./token-type.enum";
import {UserNotFoundException} from "../../exceptions/user-not-found.exception";
import {ApiConfigService} from "../../shared/config.service";
import {TokenPayloadDto} from "./token-payload.dto";
import {MailSenderService} from "../../shared/mail-sender.service";
import {UserUnauthorizedException} from "../../exceptions/unauthorized.exception";

@Injectable()
export class AuthService {
    constructor(
        private jwtService: JwtService,
        private configService: ApiConfigService,
        private userService: UserService,
        private mailSenderService: MailSenderService,
    ) {}

    async registerEmailPass(data: UserLoginDto): Promise<TokenPayloadDto> {
        var salt = generateRandomSalt();
        var passwordHash = generateHash(data.password, salt);

        await this.userService.createOneEmailPass({email: data.email, passwordHashed: passwordHash, passwordSalt: salt});

        throw new NotImplementedException();
    }

    async createAccessToken(data: {
        role: UserRoleEnum;
        userId: string;
    }): Promise<TokenPayloadDto> {
        return new TokenPayloadDto({
            expiresIn: this.configService.authConfig.jwtExpirationTime,
            accessToken: await this.jwtService.signAsync({
                userId: data.userId,
                type: TokenType.ACCESS_TOKEN,
                role: data.role,
            }),
        });
    }

    async validateUserEmailPass(userLogin: {email:string, password: string}): Promise<UserEntity> {
        const user = await this.userService.findOne({
            where: {
                email: userLogin.email,
            }
        });
        if(!user)
            throw new UserNotFoundException("User not found with email: " + userLogin.email);

        if(!user.emailValidated)
            throw new UserUnauthorizedException("Email not validated.");

        if(!user.passwordHash || !user.passwordSalt)
            throw new UserUnauthorizedException("User has no password. Use social login instead or recover password.");

        var salt = user?.passwordSalt;
        var passwordHash = generateHash(userLogin.password, salt);

        const isPasswordValid = validateHash(
            userLogin.password,
            user.passwordHash,
            user.passwordSalt,
        );

        if (!isPasswordValid)
            throw new UserUnauthorizedException("Invalid password");

        return user!;
    }
}