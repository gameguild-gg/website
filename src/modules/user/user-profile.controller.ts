import {Crud, CrudController} from "@dataui/crud";
import {Controller} from "@nestjs/common";
import {ApiTags} from "@nestjs/swagger";
import {UserProfileEntity} from "./user-profile.entity";
import {UserProfileService} from "./user-profile.service";

@Crud({
    model: {
        type: UserProfileEntity,
    },
    params: {
        id: {
            field: 'id',
            type: 'uuid',
            primary: true,
        }
    }
})
@Controller('user-profile')
@ApiTags('user-profile')
export class UserProfileController implements CrudController<UserProfileEntity>{
    constructor(public service: UserProfileService) {}

    get base(): CrudController<UserProfileEntity> {
        return this;
    }
}