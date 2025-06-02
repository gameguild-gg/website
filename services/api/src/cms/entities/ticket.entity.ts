// import { WithRolesEntity } from '../../auth/entities/with-roles.entity';
// import { ProjectEntity } from './project.entity';
// import { Entity, ManyToOne, Column } from 'typeorm';
// import { ApiProperty } from '@nestjs/swagger';
// import { IsString, IsEnum } from 'class-validator';
//
// export enum TicketStatus {
//   OPEN = 'OPEN',
//   IN_PROGRESS = 'IN_PROGRESS',
//   RESOLVED = 'RESOLVED',
//   CLOSED = 'CLOSED',
// }
//
// export enum TicketPriority {
//   LOW = 'LOW',
//   MEDIUM = 'MEDIUM',
//   HIGH = 'HIGH',
//   CRITICAL = 'CRITICAL',
// }
//
// @Entity('ticket')
// export class TicketEntity extends WithRolesEntity {
//   @ApiProperty({ type: String, description: 'Title of the ticket' })
//   @Column({ type: 'varchar', length: 255 })
//   @IsString()
//   title: string;
//
//   @ApiProperty({ type: String, description: 'Description of the ticket' })
//   @Column({ type: 'text', nullable: true })
//   @IsString()
//   description?: string;
//
//   @ApiProperty({ type: String, description: 'Description of the ticket' })
//   @Column({ type: 'text', nullable: true })
//   @IsString()
//   projectId?: string;
//
//   @ApiProperty({ enum: TicketStatus, description: 'Status of the ticket' })
//   @Column({ type: 'enum', enum: TicketStatus, default: TicketStatus.OPEN })
//   @IsEnum(TicketStatus)
//   status: TicketStatus;
//
//   @ApiProperty({ enum: TicketPriority, description: 'Priority of the ticket' })
//   @Column({
//     type: 'enum',
//     enum: TicketPriority,
//     default: TicketPriority.LOW,
//   })
//   @IsEnum(TicketPriority)
//   priority: TicketPriority;
//
//   @ManyToOne(() => ProjectEntity, (project) => project.tickets)
//   project: ProjectEntity;
//
//   // constructor(partial?: Partial<TicketEntity>) {
//   //   super(partial);
//   //   Object.assign(this, partial);
//   // }
// }
