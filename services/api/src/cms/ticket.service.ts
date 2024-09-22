import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TicketEntity, TicketStatus } from './entities/ticket.entity';
import { ProjectService } from './project.service';
import { UserService } from '../user/user.service'; // Import your UserService
import { CreateTicketDto } from './dtos/Create-Ticket.dto';

@Injectable()
export class TicketService {
  private readonly logger = new Logger(TicketService.name); // Create a logger

  constructor(
    @InjectRepository(TicketEntity)
    private readonly ticketRepository: Repository<TicketEntity>,
    private readonly projectService: ProjectService,
    private readonly userService: UserService, // Inject your UserService
  ) {}

  async createTicket(createTicketDto: CreateTicketDto): Promise<TicketEntity> {
    // Check if user with this username already exists
    const user = await this.userService.findOne({
      where: { username: 'daddy' },
    });

    const newTicket = new TicketEntity(createTicketDto);
    newTicket.owner = user;
    newTicket.owner_username = user.username;

    return this.ticketRepository.save(newTicket);
  }

  async updateTicketStatus(
    ticketID: string,
    newStatus: TicketStatus,
  ): Promise<void> {
    // Fetch the ticket by ID
    const ticket = await this.ticketRepository.findOne({
      where: { id: ticketID },
    });

    if (!ticket) {
      throw `Ticket with ID ${ticketID} not found.`;
    }

    // Update the status with the provided enum value
    ticket.status = newStatus;

    // Save the updated ticket
    await this.ticketRepository.save(ticket);
  }

  async getAllTickets(): Promise<TicketEntity[]> {
    return this.ticketRepository.find();
  }

  async deleteAllTickets(): Promise<void> {
    // Delete all tickets
    await this.ticketRepository
      .createQueryBuilder()
      .delete()
      .from(TicketEntity)
      .execute();
  }
}
