import { SetMetadata } from '@nestjs/common';
import { UserRole } from '../user-role.enum';

export const ROLE_KEY = 'requiredRole';
export const RequireRole = (Role: UserRole) => SetMetadata(ROLE_KEY, Role);
