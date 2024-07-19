import {applyDecorators, UseGuards} from '@nestjs/common';
import {Public} from "./public.decorator";
import {AccessTokenGuard} from "../guards/access-token-guard.service";


type AuthRole =
  | { isPublic: boolean }


export const Authentication = (role: AuthRole = {isPublic: false}): PropertyDecorator => {
  // SetMetadata('auth', args)

  // Os Decorators precisam ser aplicados na ordem correta, por isso a ordem é importante.

  // 1. Public() // A Rota é publica.

  // 2. AccessTokenGuard Se a rota não é Pública eu preciso aplicar o JWT Guard e obter o usuário por meio dela.

  // 3. Tenho os dados e o user no request, agora eu preciso verificar se o usuário tem permissão para acessar a rota
  // com as permissões de sistema (ADMIN, MANAGER, USER).

  // 4. Se o nível do usuário for USER, eu preciso verificar o ContentRoles, para verificar se o usuáiro
  // tem permissão para acessar a rota.

  // 5. Se o usuário não for OWNER, tem que ter permissão para acessar a rota apenas como VIEWER, eu preciso verificar se o usuário
  // é que TIPO DE VIEWER ou SUBSCRIBER (TEACHER, GUARDIAN, STUDENT).

  let decorators: MethodDecorator | ClassDecorator | PropertyDecorator[] = [];

  if (!role.isPublic) {
    // The guards must be applied in the correct order, so the order is important!

    // If the route isn't public, apply the UseGuards to get the user from JWT Guard.
    decorators.push(UseGuards(AccessTokenGuard)); // This guard will get the user from the JWT token, using the access-token strategy.
    // If the route isn't public, apply the SystemRolesGuard to check the user's system role.
    // decorators.push(UseGuards(SystemRolesGuard)); // This guard will check the user's system role.
    // // If the route isn't public, apply the ContentRolesGuard to check the user's content role.
    // decorators.push(UseGuards(ContentRolesGuard)); // This guard will check the user's content role.
    // // If the route isn't public, apply the ViewerRolesGuard to check the user's viewer role.
    // decorators.push(UseGuards(ViewerRolesGuard)); // This guard will check the user's viewer role.

  } else {
    // The route is public, apply the Public decorator
    decorators.push(Public());
  }

  return applyDecorators(...decorators);
};
