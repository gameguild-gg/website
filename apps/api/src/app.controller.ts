import { Controller, Get, HttpStatus, Redirect } from '@nestjs/common';
import { ApiExcludeEndpoint } from '@nestjs/swagger';

@Controller()
export class AppController {
  @Get()
  @Redirect('/documentation', HttpStatus.MOVED_PERMANENTLY)
  @ApiExcludeEndpoint()
  private async root() {}
}
