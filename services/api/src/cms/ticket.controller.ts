import { Controller, Post, Get, Body, Param } from '@nestjs/common';
import { TicketService } from './ticket.service';
import { TicketEntity } from './entities/ticket.entity';

@Controller('tickets')
export class TicketController {
  constructor(private readonly ticketService: TicketService) {}

  @Post('create-ticket')
  async create(
    //@Param('projectId') projectId: string,
    @Body('ticket data') ticketData: Partial<TicketEntity>,
  ): Promise<TicketEntity> {
    return this.ticketService.createTicket(ticketData);
  }
}
