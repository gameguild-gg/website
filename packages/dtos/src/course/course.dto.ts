import { OmitType } from "@nestjs/swagger";
import { CourseEntity } from "../../cms/entities/course.entity";

export class CourseDto extends OmitType(CourseEntity, ["updatedAt"]) {}
