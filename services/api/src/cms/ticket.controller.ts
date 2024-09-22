import { Controller, Post, Body } from '@nestjs/common';
import { TicketService } from './ticket.service';
import { TicketEntity } from './entities/ticket.entity';
import { CreateTicketDto } from './dtos/Create-Ticket.dto';

@Controller('tickets')
export class TicketController {
  constructor(private readonly ticketService: TicketService) {}

  @Post('create-ticket')
  async create(
    //@Param('projectId') projectId: string,
    @Body() createTicketDto: CreateTicketDto,
  ): Promise<TicketEntity> {
    return this.ticketService.createTicket(createTicketDto);
  }
}
