import {ConflictException} from '@nestjs/common';

export class UserExistsException extends ConflictException {
    constructor(error?: string) {
        super('error.userExists', error);
    }
}
