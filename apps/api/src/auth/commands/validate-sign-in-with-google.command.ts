import { Command } from '@nestjs/cqrs';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';

export class ValidateSignInWithGoogleCommand extends Command<SignInResponseDto> {
  constructor(public readonly idToken: string) {
    super();
  }
}
