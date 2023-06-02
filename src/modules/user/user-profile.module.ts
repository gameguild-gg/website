import { Module } from '@nestjs/common';
import {TypeOrmModule} from "@nestjs/typeorm";
import {UserProfileEntity} from "./user-profile.entity";
import {UserProfileController} from "./user-profile.controller";
import {UserProfileService} from "./user-profile.service";

@Module({
    imports: [TypeOrmModule.forFeature([UserProfileEntity])],
    controllers: [UserProfileController],
    exports: [UserProfileService],
    providers: [UserProfileService],
})
export class UserProfileModule {}
