import { Controller, Post, Body, Get } from '@nestjs/common';
import { GameService } from './project.service';
import { ProjectEntity } from './entities/project.entity';

@Controller('games')
export class GameController {
  constructor(private readonly gameService: GameService) {}

  @Post()
  async create(@Body() gameData: Partial<ProjectEntity>) {
    return this.gameService.create(gameData);
  }

  @Get()
  async findAll() {
    return this.gameService.findAll();
  }
}
