import { Controller, Get, HttpStatus, Redirect } from '@nestjs/common';
import { ApiExcludeEndpoint } from '@nestjs/swagger';
import { Public } from '@/auth/decorators/public.decorator';

@Controller()
export class AppController {
  @Get()
  @Public()
  @Redirect('/documentation', HttpStatus.MOVED_PERMANENTLY)
  @ApiExcludeEndpoint()
  private async root() {}
}
