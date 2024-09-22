import { Controller, Post, Body, Param, Get, Delete } from '@nestjs/common';
import { TicketService } from './ticket.service';
import { TicketEntity, TicketStatus } from './entities/ticket.entity';
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
  @Post('Update ticket Status')
  async updateStatus(
    @Param('Ticket ID') ticketID: string,
    newStatus: TicketStatus,
  ): Promise<void> {
    this.ticketService.updateTicketStatus(ticketID, newStatus);
  }
  @Get()
  async getAll(): Promise<TicketEntity[]> {
    return this.ticketService.getAllTickets();
  }
  @Delete('delete-all') // Adjust the endpoint as needed
  async deleteAllTickets(): Promise<void> {
    await this.ticketService.deleteAllTickets();
  }
}
