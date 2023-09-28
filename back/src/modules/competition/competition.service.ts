import { Injectable } from '@nestjs/common';
import { UserService } from '../user/user.service';

@Injectable()
export class CompetitionService {
  constructor(private userService: UserService) {}
}
