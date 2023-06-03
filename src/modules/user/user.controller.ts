import {Crud, CrudController} from "@dataui/crud";
import {Controller} from "@nestjs/common";
import {ApiTags} from "@nestjs/swagger";
import {UserEntity} from "./user.entity";
import {UserService} from "./user.service";

@Crud({
    model: {
        type: UserEntity,
    },
    params: {
        id: {
            field: 'id',
            type: 'uuid',
            primary: true,
        }
    },
    routes: {
        only: ['getOneBase'],
    }
})
@Controller('user')
@ApiTags('user')
export class UserController implements CrudController<UserEntity>{
    constructor(public service: UserService) {}

    get base(): CrudController<UserEntity> {
        return this;
    }
}