import { LocalSignInRequest } from '@/auth/dtos/local-sign-in-request.dto';

export class LocalSignInCommand {
  constructor(public readonly data: LocalSignInRequest) {}
}
