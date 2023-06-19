import {ConflictException, InternalServerErrorException, UnauthorizedException} from '@nestjs/common';

export class InternalServerErrorServerMisconfigurationException extends InternalServerErrorException {
    constructor(description?: string) {
        super('error.serverMisconfigured', description);
    }
}