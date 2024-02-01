import { Injectable } from '@nestjs/common';
import {UserProfileEntity} from "./entities/user-profile.entity";
import {InjectRepository} from "@nestjs/typeorm";
import {Repository} from "typeorm";

@Injectable()
export class UserProfileService {
  constructor(
    @InjectRepository(UserProfileEntity)
    private readonly repository: Repository<UserProfileEntity>
  ) {
    
  }
}
