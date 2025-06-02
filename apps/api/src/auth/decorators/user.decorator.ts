import { createParamDecorator, ExecutionContext } from '@nestjs/common';

import { UserDto } from '@/user/dtos/user.dto';

export function User(): ParameterDecorator;
export function User<K extends keyof UserDto>(property: K): ParameterDecorator;
export function User<K extends keyof UserDto>(property: K[]): ParameterDecorator;
export function User<K extends keyof UserDto>(property?: K | K[]): ParameterDecorator {
  return createParamDecorator((data: unknown, ctx: ExecutionContext): UserDto | UserDto[K] | Pick<UserDto, K> => {
    const request = ctx.switchToHttp().getRequest();
    const user = request.user as UserDto;

    if (!property) return user;

    if (Array.isArray(property)) {
      const result = {} as Pick<UserDto, K>;
      for (const key of property) {
        if (!(key in user)) throw new Error(`The property '${key}' does not exist in UserDto`);
        result[key] = user[key];
      }
      return result;
    }

    if (!(property in user)) throw new Error(`The property '${property}' does not exist in UserDto`);

    return user[property];
  })(property);
}
