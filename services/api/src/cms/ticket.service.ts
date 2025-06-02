// // ticket.service.ts
// import { Injectable } from '@nestjs/common';
// import { TypeOrmCrudService } from '@dataui/crud-typeorm';
// import { InjectRepository } from '@nestjs/typeorm';
// import { TicketEntity } from './entities/ticket.entity';
// import { Repository } from 'typeorm';
// import { ProjectEntity } from './entities/project.entity';
//
// @Injectable()
// export class TicketService extends TypeOrmCrudService<TicketEntity> {
//   constructor(
//     @InjectRepository(TicketEntity) ticketRepo: Repository<TicketEntity>,
//     @InjectRepository(ProjectEntity)
//     private readonly gameRepository: Repository<ProjectEntity>,
//   ) {
//     super(ticketRepo);
//   }
//   save(ticket: TicketEntity): Promise<ProjectEntity> {
//     this.gameRepository.save(ticket);
//     return this.gameRepository.findOne({ where: { id: ticket.id } });
//   }
// }
