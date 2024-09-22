import { WithRolesEntity } from '../../auth/entities/with-roles.entity';
import { Controller, Post, Get, Body, Param } from '@nestjs/common';
import { ProjectEntity } from './project.entity';
import { Entity, ManyToOne, Column, PrimaryGeneratedColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsNotEmpty, IsEnum, IsDate } from 'class-validator';

export enum TicketStatus {
  OPEN = 'OPEN',
  IN_PROGRESS = 'IN_PROGRESS',
  RESOLVED = 'RESOLVED',
  CLOSED = 'CLOSED',
}

export enum TicketPriority {
  LOW = 'LOW',
  MEDIUM = 'MEDIUM',
  HIGH = 'HIGH',
  CRITICAL = 'CRITICAL',
}

@Entity('ticket')
export class TicketEntity extends WithRolesEntity {
  @PrimaryGeneratedColumn()
  ticketNumber: number;

  @ApiProperty({ type: String, description: 'Title of the ticket' })
  @Column({ type: 'varchar', length: 255 })
  @IsString()
  @IsNotEmpty()
  title: string;

  @ApiProperty({ type: String, description: 'Description of the ticket' })
  @Column({ type: 'text', nullable: true })
  @IsString()
  description?: string;

  @ApiProperty({ enum: TicketStatus, description: 'Status of the ticket' })
  @Column({ type: 'enum', enum: TicketStatus, default: TicketStatus.OPEN })
  @IsEnum(TicketStatus)
  status: TicketStatus;

  @ApiProperty({ enum: TicketPriority, description: 'Priority of the ticket' })
  @Column({
    type: 'enum',
    enum: TicketPriority,
    default: TicketPriority.LOW,
  })
  @IsEnum(TicketPriority)
  priority: TicketPriority;

  @ApiProperty({ type: Date, description: 'Date when the ticket was created' })
  @Column({ type: 'timestamp', default: () => 'CURRENT_TIMESTAMP' })
  @IsDate()
  createdAt: Date;

  @ApiProperty({
    type: Date,
    description: 'Date when the ticket was last updated',
  })
  @Column({ type: 'timestamp', nullable: false, onUpdate: 'CURRENT_TIMESTAMP' })
  @IsDate()
  updatedAt: Date;

  @ApiProperty({
    type: ProjectEntity,
    description: 'Project associated with the ticket',
  })
  @ManyToOne(() => ProjectEntity, (project) => project.tickets)
  project: ProjectEntity;

  constructor(partial?: Partial<TicketEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
