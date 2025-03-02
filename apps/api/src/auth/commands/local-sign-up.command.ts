import { Command } from '@nestjs/cqrs';
import { LocalSignUpRequestDto } from '@/auth/dtos/local-sign-up-request.dto';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

export class LocalSignUpCommand extends Command<SignInResponseDto> {
  constructor(public readonly data: LocalSignUpRequestDto) {
    super();
  }
}
