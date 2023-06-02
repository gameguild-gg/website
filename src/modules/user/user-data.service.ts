import {Injectable} from "@nestjs/common";
import {TypeOrmCrudService} from "@dataui/crud-typeorm";
import {InjectRepository} from "@nestjs/typeorm";
import {Repository} from "typeorm";
import {UserDataEntity} from "./user-data.entity";

@Injectable()
export class UserDataService extends TypeOrmCrudService<UserDataEntity> {
    constructor(
        @InjectRepository(UserDataEntity)
        private repository: Repository<UserDataEntity>,
    ) {
        super(repository);
    }
}