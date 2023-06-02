import { Module } from '@nestjs/common';
import {TypeOrmModule} from "@nestjs/typeorm";
import {UserDataEntity} from "./user-data.entity";
import {UserDataController} from "./user-data.controller";
import {UserDataService} from "./user-data.service";

@Module({
    imports: [TypeOrmModule.forFeature([UserDataEntity])],
    controllers: [UserDataController],
    exports: [UserDataService],
    providers: [UserDataService],
})
export class UserDataModule {}
