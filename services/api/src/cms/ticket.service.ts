import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TicketEntity } from './entities/ticket.entity';
import { ProjectEntity } from './entities/project.entity';

@Injectable()
export class TicketService {
  constructor(
    @InjectRepository(TicketEntity)
    private readonly ticketRepository: Repository<TicketEntity>,
    @InjectRepository(ProjectEntity)
    private readonly projectRepository: Repository<ProjectEntity>,
  ) {}

  async create(
    ticketData: Partial<TicketEntity>,
    projectId: string,
  ): Promise<TicketEntity> {
    const project = await this.projectRepository.findOne({
      where: { id: projectId },
    });
    if (!project) {
      throw new NotFoundException('Project not found');
    }

    const ticket = this.ticketRepository.create({
      ...ticketData,
      project,
    });

    return this.ticketRepository.save(ticket);
  }

  async findAll(): Promise<TicketEntity[]> {
    return this.ticketRepository.find({ relations: ['project'] });
  }
}
