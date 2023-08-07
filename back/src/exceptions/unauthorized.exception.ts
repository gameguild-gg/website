import {ConflictException, UnauthorizedException} from '@nestjs/common';

export class UserUnauthorizedException extends UnauthorizedException {
    constructor(description?: string) {
        super('error.userUnauthorized', description);
    }
}