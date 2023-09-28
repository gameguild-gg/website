import { Controller, Get } from '@nestjs/common';
import { RootService } from './root.service';

@Controller()
export class RootController {
  constructor(private readonly rootService: RootService) {}

  @Get()
  root(): string {
    // return a html page to redirect to the documentation
    return '<html><head><meta http-equiv="refresh" content="0; url=/documentation"></head></html>';
  }
}
