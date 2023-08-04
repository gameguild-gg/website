import {Injectable} from "@nestjs/common";
import {TypeOrmCrudService} from "@dataui/crud-typeorm";
import {InjectRepository} from "@nestjs/typeorm";
import {Repository} from "typeorm";
import {UserEntity} from "./user.entity";
import {UserExistsException} from "../../exceptions/user-exists.exception";
import {UserRoleEnum} from "./user-role.enum";
import {UserNotFoundException} from "../../exceptions/user-not-found.exception";

@Injectable()
export class UserService extends TypeOrmCrudService<UserEntity> {
    constructor(
        @InjectRepository(UserEntity)
        private repository: Repository<UserEntity>,
    ) {
        super(repository);
    }

    async emailExists(email: string): Promise<boolean> {
        return !!(await this.findOne({
            where: {
                email,
            },
        }));
    }

    async createOneEmailPass(data :{email: string, passwordHashed: string, passwordSalt: string}){
        if(await this.emailExists(data.email))
            throw new UserExistsException('Email already exists');

        var user = this.repository.save({
            email: data.email,
            passwordHashed: data.passwordHashed,
            passwordSalt: data.passwordSalt,
            role: UserRoleEnum.COMMON,
            emailVerified: false,
        });


        return user;
    }

    async markEmailAsValid(id: string) {
        var user = await this.findOne({
            where: {id}
        });
        if(user){
            user.emailValidated = true;
            await this.repository.save(user);
            return true;
        }
        else
            throw new UserNotFoundException("User not found with id: " + id);
    }
}