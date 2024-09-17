import { Controller, Post, Get, Body, Param } from '@nestjs/common';
import { TicketService } from './ticket.service';
import { TicketEntity } from './entities/ticket.entity';

@Controller('tickets')
export class TicketController {
  constructor(private readonly ticketService: TicketService) {}

  @Post(':projectId')
  async create(
    @Param('projectId') projectId: string,
    @Body() ticketData: Partial<TicketEntity>,
  ): Promise<TicketEntity> {
    return this.ticketService.create(ticketData, projectId);
  }

  @Get()
  async findAll(): Promise<TicketEntity[]> {
    return this.ticketService.findAll();
  }
}
