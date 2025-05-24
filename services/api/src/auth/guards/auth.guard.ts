import { AuthGuard as NestAuthGuard, type IAuthGuard, type Type } from '@nestjs/passport';

export enum AuthType {
  Public = 'public',
  AccessToken = 'access-token',
  RefreshToken = 'refresh-token',
}

export function AuthGuard(options: AuthType): Type<IAuthGuard> {
  const strategies: string[] = [];
  strategies.push(options.toString());
  return NestAuthGuard(strategies); // it is returning the correct guard
}
