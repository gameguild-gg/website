import {Body, Controller, Post} from '@nestjs/common';
import {AuthService} from "./auth.service";
import {ApiTags} from "@nestjs/swagger";
import {UserLoginDto} from "./user-login.dto";
import {TokenPayloadDto} from "./token-payload.dto";

@Controller('api/auth')
@ApiTags('auth')
export class AuthController {
    constructor(public service: AuthService) {}

    @Post('register/email-password')
    public async registerEmailPassword(@Body() data: UserLoginDto): Promise<TokenPayloadDto> {
        return await this.service.registerEmailPass(data);
    }
}
