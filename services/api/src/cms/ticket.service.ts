import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TicketEntity } from './entities/ticket.entity';
import { ProjectService } from './project.service';
import { CreateTicketDto } from './dtos/Create-Ticket.dto';

@Injectable()
export class TicketService {
  constructor(
    @InjectRepository(TicketEntity)
    private readonly ticketRepository: Repository<TicketEntity>,
    private readonly ProjectService: ProjectService,
  ) {}

  async createTicket(
    createTicketDto: CreateTicketDto, // Add ownerId as a parameter
  ): Promise<TicketEntity> {
    const newTicket = new TicketEntity(createTicketDto);
    newTicket.owner = {
      posts: null,
      courses: null,
      createdAt: null,
      updatedAt: null,
      walletAddress: 'N/A',
      profile: null,
      competitionSubmissions: null,
      elo: null,
      githubId: 'N/A',
      appleId: 'N/A',
      linkedinId: 'N/A',
      twitterId: 'N/A',
      passwordHash: '1232131231',
      passwordSalt: 'N/A',
      facebookId: 'N/A',
      googleId: 'N/a',
      username: 'daddy',
      email: 'gkingof9@gmail.com',
      emailVerified: true,
      id: 'yes',
    };
    return this.ticketRepository.save(newTicket);
  }
}
