import { Controller, Get, Redirect } from '@nestjs/common';
import { ApiExcludeEndpoint } from '@nestjs/swagger';
import { Public } from "./auth";

@Controller()
export class AppController {
  @Get()
  @Redirect('/documentation', 302)
  @ApiExcludeEndpoint()
  @Public()
  root() {}
}
