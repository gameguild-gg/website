import { Injectable, NotFoundException, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TicketEntity } from './entities/ticket.entity';
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
    newTicket.owner = user; // Assign the existing or newly created user as the owner

    return this.ticketRepository.save(newTicket);
  }
}
