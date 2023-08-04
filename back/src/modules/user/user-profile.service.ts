import {Injectable} from "@nestjs/common";
import {TypeOrmCrudService} from "@dataui/crud-typeorm";
import {InjectRepository} from "@nestjs/typeorm";
import {Repository} from "typeorm";
import {UserProfileEntity} from "./user-profile.entity";

@Injectable()
export class UserProfileService extends TypeOrmCrudService<UserProfileEntity> {
    constructor(
        @InjectRepository(UserProfileEntity)
        private repository: Repository<UserProfileEntity>,
    ) {
        super(repository);
    }
}