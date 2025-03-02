import { Command } from '@nestjs/cqrs';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

export class ValidateLocalSignInCommand extends Command<SignInResponseDto> {
  constructor(public readonly data: LocalSignInRequestDto) {
    super();
  }
}
