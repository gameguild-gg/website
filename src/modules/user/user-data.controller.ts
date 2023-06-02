import {Crud, CrudController} from "@dataui/crud";
import {Controller} from "@nestjs/common";
import {ApiTags} from "@nestjs/swagger";
import {UserDataEntity} from "./user-data.entity";
import {UserDataService} from "./user-data.service";

@Crud({
    model: {
        type: UserDataEntity,
    },
    params: {
        id: {
            field: 'id',
            type: 'uuid',
            primary: true,
        }
    }
})
@Controller('user-data')
@ApiTags('user-data')
export class UserDataController implements CrudController<UserDataEntity>{
    constructor(public service: UserDataService) {}

    get base(): CrudController<UserDataEntity> {
        return this;
    }
}