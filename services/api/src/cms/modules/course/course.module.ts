import { Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { CourseEntity } from "../../entities/course.entity";
import { CourseService } from "./course.service";
import { CourseController } from "./course.controller";

@Module({
    imports: [
        TypeOrmModule.forFeature([CourseEntity]),
    ],
    controllers: [CourseController],
    providers: [CourseService],
    exports: [CourseService],
})
export class CouseModule {}
