import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TicketEntity } from './entities/ticket.entity';
import { ProjectEntity } from './entities/project.entity';
import { ProjectService } from './project.service';

@Injectable()
export class TicketService {
  constructor(
    @InjectRepository(TicketEntity)
    private readonly ticketRepository: Repository<TicketEntity>,
    private readonly ProjectService: ProjectService,
  ) {}

  async createTicketWithProjcet(
    ticketData: Partial<TicketEntity>,
    projectId: string,
  ) {
    const project = await this.ProjectService.findOne({
      where: { id: projectId },
    });
    if (!project) {
      throw 'No Project with that id';
    }
    const ticket = this.ticketRepository.create({
      ...ticketData,
      project,
    });
    return this.ticketRepository.save(ticket);
  }
}
