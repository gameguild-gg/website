import { Controller, Get, Patch, Post } from '@nestjs/common';
import { Crud } from '@dataui/crud';
import { ChoiceCastService } from './choice-cast.service';
import { EventEntity } from '../entities/event.entity';
import { ApiTags } from '@nestjs/swagger';
import { AuthenticatedRoute, EditorRoute, OwnerRoute } from 'src/auth/auth.enum';
import { Auth } from 'src/auth';
import { OwnershipEmptyInterceptor } from 'src/cms/interceptors/ownership-empty-interceptor.service';
import { CreateChoiceCastDto } from './dto/CreateChoiceCast.dto';

@Crud({
  model: {
    type: EventEntity,
  },
  params: {
    slug: {
      field: 'slug',
      type: 'string',
      primary: true,
    },
  },
  dto: {
    create: CreateChoiceCastDto,
  },
  routes: {
    exclude: [
        'replaceOneBase',
        'createManyBase',
        'createManyBase',
        'recoverOneBase',
    ],
    getOneBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    createOneBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    getManyBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    updateOneBase: {
      decorators: [Auth<EventEntity>(EditorRoute<EventEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<EventEntity>(OwnerRoute<EventEntity>)],
    },
  },
})
@ApiTags("Choice Casts")
@Controller("choice-cast")
export class ChoiceCastController {
  constructor(readonly service: ChoiceCastService) {}

  @Patch("submit-cast/:id")
  async submitChoiceCast() {
    return this.service.submitChoiceCast();
  }
  
  @Patch("reset-vote/:id")
  async resetVote() {
    return this.service.resetVote();
  }
  
  @Patch("vote-in-cast/:castId")
  async voteForChoiceCast() {
    return this.service.voteForChoiceCast();
  }
  
  @Get("get-casts-from-user/:userId")
  async getChoiceCastFromUser() {
    return this.service.getChoiceCastFromUser();
  }
  
  @Get("get-votes-from-cast/:castId")
  async getVotesForChoiceCast() {
    return this.service.getVotesForChoiceCast();
  }
  
  @Post("vote")
  async getChoiceCastCandidates() {
    return this.service.getChoiceCastCandidates();
  }
}
