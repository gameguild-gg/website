import { Injectable, NestMiddleware } from '@nestjs/common';
import { NextFunction } from 'express';
import { UsersService } from 'src/users/users.service';

@Injectable()
export class SignUpValidationMiddleware implements NestMiddleware {
  constructor(private readonly usersService: UsersService) {}
  async use(req, res, next: NextFunction) {
    const { email } = req.body;
    const user = await this.usersService.findOne({ email });

    if (user) {
      throw new Error('Email already in use');
    }
    console.log('here');

    next();
  }
}
