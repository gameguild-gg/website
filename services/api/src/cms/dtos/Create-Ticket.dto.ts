import { IsString, IsEnum, IsNotEmpty } from 'class-validator';
import { TicketStatus, TicketPriority } from '../entities/ticket.entity';
import { UserEntity } from '../../user/entities';

export class CreateTicketDto {
  @IsString()
  @IsNotEmpty()
  title: string;

  @IsString()
  description: string;

  @IsEnum(TicketStatus)
  status: TicketStatus;

  @IsEnum(TicketPriority)
  priority: TicketPriority;

  @IsString()
  projectId: string;

  @IsNotEmpty()
  owner: UserEntity;
}
