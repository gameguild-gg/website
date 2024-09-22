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
    ticketData: Partial<TicketEntity>,
    // projectId: string,
  ): Promise<TicketEntity> {
    //const project = await this.ProjectService.findOne({
    // where: { id: projectId },
    // });
    //if (!project) {
    // throw new NotFoundException();
    // }
    const newTicket = new TicketEntity(ticketData);
    //newTicket.project = project; // Set the project
    return this.ticketRepository.save(newTicket);
  }
}
