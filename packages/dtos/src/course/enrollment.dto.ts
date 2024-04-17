import { IntersectionType, PickType } from "@nestjs/swagger";
import { UserDto } from "../user/user.dto";
import { CourseDto } from "./course.dto";

export class EnrollmentDto extends IntersectionType(
    PickType(UserDto, ["username", "email"]),
    CourseDto
) {}
