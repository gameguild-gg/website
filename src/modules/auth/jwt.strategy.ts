import { Injectable, UnauthorizedException } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';

import type { UserEntity } from '../user/user.entity';
import { UserService } from '../user/user.service';
import {TokenType} from "./token-type.enum";
import {UserRoleEnum} from "../user/user-role.enum";

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {
    constructor(
        // private configService: ApiConfigService,
        private userService: UserService,
    ) {
        super({
            jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
            // secretOrKey: configService.authConfig.publicKey, // todo: fix this
        });
    }

    // todo: fix this
    // async validate(args: {
    //     userId: string;
    //     role: UserRoleEnum;
    //     type: TokenType;
    // }): Promise<UserEntity> {
    //     if (args.type !== TokenType.ACCESS_TOKEN) {
    //         throw new UnauthorizedException();
    //     }
    //
    //     const user = await this.userService.findOne({
    //         where: {
    //             id: args.userId,
    //             role: args.role,
    //         },
    //     });
    //
    //     if (!user) {
    //         throw new UnauthorizedException();
    //     }
    //
    //     return user;
    // }
}