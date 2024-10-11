import { createParamDecorator, ExecutionContext, BadRequestException } from "@nestjs/common";
import { JobAplicationEntity } from "../entities/job-aplication.entity";
import { UserEntity } from "src/user/entities";


export const BodyAplicantInject = createParamDecorator(
    (data: unknown, ctx: ExecutionContext) => {
      const request = ctx.switchToHttp().getRequest();
  
      const user = request.user as UserEntity;
      if (!user) {
        throw new BadRequestException(
          'error.user: User not found in the context, have you missed the AuthUserInterceptor?',
        );
      }
  
      if (request.body) {
        (request.body as JobAplicationEntity).aplicant = user;
      } else
        throw new BadRequestException(
          'error.body: Request body not found in the context.',
        );
  
      // Otherwise, return the entire body
      return request.body;
    },
  );