import {ApiProperty} from '@nestjs/swagger';
import {IsEmail, IsString, IsStrongPassword} from 'class-validator';

export class UserLoginDto {
    @IsString()
    @IsEmail({}, {message: 'error.emailInvalid'})
    @ApiProperty()
    readonly email: string;

    @IsStrongPassword(
        {
            minLength: 8,
            minLowercase: 1,
            minUppercase: 1,
            minNumbers: 1,
            minSymbols: 1,
        },
        {
            message:
                'error.password: too weak. Needs at least 8 characters. At least 1 lowercase, 1 uppercase, 1 number and 1 symbol',
        },
    )
    @ApiProperty()
    readonly password: string;
}
