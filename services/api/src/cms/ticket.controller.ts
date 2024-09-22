import {
  Controller,
  Post,
  Put,
  Body,
  Param,
  Get,
  Delete,
} from '@nestjs/common';
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
  @Put(':ticketID/status') // Ensure the route matches this pattern
  async updateStatus(
    @Param('ticketID') ticketID: string,
    @Body('newStatus') newStatus: TicketStatus, // Use @Body for the new status
  ): Promise<void> {
    console.log(newStatus);
    await this.ticketService.updateTicketStatus(ticketID, newStatus);
  }
  @Get()
  async getAll(): Promise<TicketEntity[]> {
    return this.ticketService.getAllTickets();
  }

  @Get('tickets/:ticketID')
  async getTicketByID(
    @Param('ticketID') ticketID: string,
  ): Promise<TicketEntity> {
    return this.ticketService.getTicketID(ticketID);
  }
  @Delete('delete-all') // Adjust the endpoint as needed
  async deleteAllTickets(): Promise<void> {
    await this.ticketService.deleteAllTickets();
  }
}
