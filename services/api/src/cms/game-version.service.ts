import {
  Injectable,
  Logger,
  NotFoundException,
  UnauthorizedException,
} from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { ArrayContains, Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { ProjectVersionEntity } from './entities/project-version.entity';
import { UserEntity } from '../user/entities';
import { ProjectEntity } from './entities/project.entity';

@Injectable()
export class GameVersionService extends TypeOrmCrudService<ProjectVersionEntity> {
  private readonly logger = new Logger(GameVersionService.name);
  constructor(
    @InjectRepository(ProjectVersionEntity)
    private readonly gameVersionRepository: Repository<ProjectVersionEntity>,
    @InjectRepository(ProjectEntity)
    private readonly gameRepository: Repository<ProjectEntity>,
  ) {
    super(gameVersionRepository);
  }

  async createGameVersion(
    body: ProjectVersionEntity,
    user: UserEntity,
  ): Promise<ProjectVersionEntity> {
    if (!body.project || !body.project.id) {
      throw new NotFoundException('{ game: { id: string } } field is required');
    }
    // check if user is owner or editor of the game
    const game = await this.gameRepository.findOne({
      where: [
        // is owner
        { id: body.project.id, owner: user },
        // or is one of the editors
        { id: body.project.id, editors: ArrayContains([user.id]) },
      ],
    });
    // check if the user can create a version of the game
    if (!game) {
      throw new UnauthorizedException(
        'User is not the owner or editor of the game',
      );
    }
    // create the version
    const version = this.gameVersionRepository.create(body);
    version.project = game;
    return this.gameVersionRepository.save(version);
  }
}
