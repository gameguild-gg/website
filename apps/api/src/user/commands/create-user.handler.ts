// import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
// import { Logger } from '@nestjs/common';
// import { UserDto } from '@/user/dtos/user.dto';
// import { UserService } from '@/user/services/user.service';
// import { CreateUserCommand } from '@/user/commands/create-user.command';
// import { UserEntity } from '@/user/entities/user.entity';
//
// @CommandHandler(CreateUserCommand)
// export class CreateUserHandler implements ICommandHandler<CreateUserCommand> {
//   private readonly logger = new Logger(CreateUserHandler.name);
//
//   constructor(
//     private readonly userService: UserService,
//     private readonly publisher: EventPublisher,
//   ) {}
//
//   public async execute(command: CreateUserCommand): Promise<UserDto> {
//     // const { name, email } = command.data;
//     // // Cria a entidade e persiste no banco
//     const userEntity: UserEntity = await this.userService.createUser(command.data);
//     // const user: UserEntity = this.userRepository.create({ name, email });
//     // await this.userRepository.save(user);
//     // Se o User estender AggregateRoot, você pode mesclar o contexto para emitir eventos:
//     // const userContext = this.publisher.mergeObjectContext(user);
//     // userContext.apply(new UserCreatedEvent(user.id, user.name, user.email));
//     // userContext.commit();
//     // Retorne os dados do usuário criado para que o resolver possa usá-los
//     // return { id: user.id, name: user.name, email: user.email };
//     return null;
//   }
// }
