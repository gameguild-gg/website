import {
  Controller,
  Get,
  Post,
  Put,
  Delete,
  Body,
  Param,
  Query,
  UseGuards,
  Request,
  HttpStatus,
  HttpException,
  ParseUUIDPipe,
  ValidationPipe,
} from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse, ApiParam, ApiQuery, ApiBearerAuth } from '@nestjs/swagger';
import { IsString, IsOptional, IsBoolean, IsNumber, IsArray, Min, Max, IsEnum } from 'class-validator';

import { ProgramService } from '../services/program.service';
import { CreateProgramDto, UpdateProgramDto, ProgramFilters } from '../services/dtos';
import { ProgramRoleType } from '../entities/enums';
import { Program } from '../entities';

// DTOs for API validation
export class CreateProgramRequestDto implements CreateProgramDto {
  @IsString()
  slug: string;

  @IsString()
  summary: string;

  @IsOptional()
  body: object;

  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  tenancyDomains?: string[];

  @IsOptional()
  metadata?: object;
}

export class UpdateProgramRequestDto implements UpdateProgramDto {
  @IsOptional()
  @IsString()
  slug?: string;

  @IsOptional()
  @IsString()
  summary?: string;

  @IsOptional()
  body?: object;

  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  tenancyDomains?: string[];

  @IsOptional()
  metadata?: object;
}

export class ProgramFiltersDto implements ProgramFilters {
  @IsOptional()
  @IsString()
  difficulty_level?: string;

  @IsOptional()
  @IsBoolean()
  is_public?: boolean;

  @IsOptional()
  @IsBoolean()
  is_active?: boolean;

  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  tags?: string[];

  @IsOptional()
  @IsString()
  search?: string;
}

export class EnrollmentRequestDto {
  @IsOptional()
  @IsString()
  invitation_code?: string;

  @IsOptional()
  @IsString()
  message?: string;
}

export class AssignRoleDto {
  @IsEnum(ProgramRoleType)
  role: ProgramRoleType;
}

// Helper function to handle error responses
function handleError(error: unknown): never {
  const errorMessage = error instanceof Error ? error.message : 'An unknown error occurred';
  
  if (errorMessage.includes('not found')) {
    throw new HttpException(errorMessage, HttpStatus.NOT_FOUND);
  }
  if (errorMessage.includes('permission')) {
    throw new HttpException(errorMessage, HttpStatus.FORBIDDEN);
  }
  if (errorMessage.includes('already exists') || errorMessage.includes('already linked')) {
    throw new HttpException(errorMessage, HttpStatus.BAD_REQUEST);
  }
  
  throw new HttpException(errorMessage, HttpStatus.INTERNAL_SERVER_ERROR);
}

@ApiTags('Programs')
@Controller('programs')
// @ApiBearerAuth() // Uncomment when auth is implemented
export class ProgramController {
  constructor(private readonly programService: ProgramService) {}

  // Core CRUD Operations
  @Post()
  @ApiOperation({ summary: 'Create a new program' })
  @ApiResponse({ status: 201, description: 'Program created successfully', type: Program })
  @ApiResponse({ status: 400, description: 'Bad request - validation errors' })
  @ApiResponse({ status: 401, description: 'Unauthorized' })
  async create(@Body(ValidationPipe) createProgramDto: CreateProgramRequestDto, @Request() req: any): Promise<Program> {
    try {
      // In a real implementation, get user ID from JWT token
      const userId = req.user?.id || 'mock-user-id';
      return await this.programService.create(createProgramDto, userId);
    } catch (error) {
      handleError(error);
    }
  }

  @Get()
  @ApiOperation({ summary: 'Get all programs with optional filtering' })
  @ApiQuery({ name: 'difficulty_level', required: false, description: 'Filter by difficulty level' })
  @ApiQuery({ name: 'is_public', required: false, description: 'Filter by public/private programs' })
  @ApiQuery({ name: 'is_active', required: false, description: 'Filter by active/inactive programs' })
  @ApiQuery({ name: 'search', required: false, description: 'Search in title and description' })
  @ApiQuery({ name: 'tags', required: false, description: 'Filter by tags (comma-separated)' })
  @ApiResponse({ status: 200, description: 'Programs retrieved successfully', type: [Program] })
  async findAll(@Query() filters: ProgramFiltersDto): Promise<Program[]> {
    try {
      // Parse tags if provided as comma-separated string
      if (typeof filters.tags === 'string') {
        filters.tags = (filters.tags as string).split(',').map((tag) => tag.trim());
      }

      return await this.programService.findAll(filters);
    } catch (error) {
      handleError(error);
    }
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a program by ID' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 200, description: 'Program retrieved successfully', type: Program })
  @ApiResponse({ status: 404, description: 'Program not found' })
  async findOne(@Param('id', ParseUUIDPipe) id: string): Promise<Program> {
    try {
      return await this.programService.findOne(id);
    } catch (error) {
      handleError(error);
    }
  }

  @Put(':id')
  @ApiOperation({ summary: 'Update a program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 200, description: 'Program updated successfully', type: Program })
  @ApiResponse({ status: 404, description: 'Program not found' })
  @ApiResponse({ status: 403, description: 'Forbidden - insufficient permissions' })
  async update(@Param('id', ParseUUIDPipe) id: string, @Body(ValidationPipe) updateProgramDto: UpdateProgramRequestDto, @Request() req: any): Promise<Program> {
    try {
      const userId = req.user?.id || 'mock-user-id';
      return await this.programService.update(id, updateProgramDto, userId);
    } catch (error) {
      handleError(error);
    }
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete (deactivate) a program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 200, description: 'Program deleted successfully' })
  @ApiResponse({ status: 404, description: 'Program not found' })
  @ApiResponse({ status: 403, description: 'Forbidden - insufficient permissions' })
  async remove(@Param('id', ParseUUIDPipe) id: string, @Request() req: any): Promise<{ message: string }> {
    try {
      const userId = req.user?.id || 'mock-user-id';
      await this.programService.remove(id, userId);
      return { message: 'Program deleted successfully' };
    } catch (error) {
      handleError(error);
    }
  }

  // Enrollment Management
  @Post(':id/enroll')
  @ApiOperation({ summary: 'Enroll in a program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 201, description: 'Enrolled successfully' })
  @ApiResponse({ status: 400, description: 'Enrollment failed' })
  @ApiResponse({ status: 404, description: 'Program not found' })
  async enroll(
    @Param('id', ParseUUIDPipe) programId: string,
    @Body(ValidationPipe) enrollmentDto: EnrollmentRequestDto,
    @Request() req: any,
  ): Promise<{ message: string; enrollment: any }> {
    try {
      const userId = req.user?.id || 'mock-user-id';
      const enrollment = await this.programService.enrollUser(programId, userId);
      return {
        message: 'Enrolled successfully',
        enrollment,
      };
    } catch (error) {
      handleError(error);
    }
  }

  @Delete(':id/enroll')
  @ApiOperation({ summary: 'Unenroll from a program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 200, description: 'Unenrolled successfully' })
  @ApiResponse({ status: 404, description: 'Program or enrollment not found' })
  async unenroll(@Param('id', ParseUUIDPipe) programId: string, @Request() req: any): Promise<{ message: string }> {
    try {
      const userId = req.user?.id || 'mock-user-id';
      await this.programService.unenrollUser(programId, userId);
      return { message: 'Unenrolled successfully' };
    } catch (error) {
      handleError(error);
    }
  }

  // Role Management
  @Post(':id/users/:userId/roles')
  @ApiOperation({ summary: 'Assign role to user in program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiParam({ name: 'userId', description: 'User UUID' })
  @ApiResponse({ status: 201, description: 'Role assigned successfully' })
  @ApiResponse({ status: 403, description: 'Insufficient permissions' })
  async assignRole(
    @Param('id', ParseUUIDPipe) programId: string,
    @Param('userId', ParseUUIDPipe) userId: string,
    @Body(ValidationPipe) roleDto: AssignRoleDto,
    @Request() req: any,
  ): Promise<{ message: string; role: any }> {
    try {
      const requestUserId = req.user?.id || 'mock-user-id';

      // Check if requesting user has permission to assign roles
      const hasPermission = await this.programService.checkUserPermission(programId, requestUserId, [ProgramRoleType.INSTRUCTOR, ProgramRoleType.ADMINISTRATOR]);

      if (!hasPermission) {
        throw new HttpException('Insufficient permissions to assign roles', HttpStatus.FORBIDDEN);
      }

      const role = await this.programService.assignUserRole(programId, userId, roleDto.role);
      return {
        message: 'Role assigned successfully',
        role,
      };
    } catch (error) {
      handleError(error);
    }
  }

  @Delete(':id/users/:userId/roles/:role')
  @ApiOperation({ summary: 'Remove role from user in program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiParam({ name: 'userId', description: 'User UUID' })
  @ApiParam({ name: 'role', description: 'Role name' })
  @ApiResponse({ status: 200, description: 'Role removed successfully' })
  @ApiResponse({ status: 403, description: 'Insufficient permissions' })
  async removeRole(
    @Param('id', ParseUUIDPipe) programId: string,
    @Param('userId', ParseUUIDPipe) userId: string,
    @Param('role') role: string,
    @Request() req: any,
  ): Promise<{ message: string }> {
    try {
      const requestUserId = req.user?.id || 'mock-user-id';

      const hasPermission = await this.programService.checkUserPermission(programId, requestUserId, [ProgramRoleType.INSTRUCTOR, ProgramRoleType.ADMINISTRATOR]);

      if (!hasPermission) {
        throw new HttpException('Insufficient permissions to remove roles', HttpStatus.FORBIDDEN);
      }

      // Convert string to enum
      const roleType = role as ProgramRoleType;
      await this.programService.removeUserRole(programId, userId, roleType);
      return { message: 'Role removed successfully' };
    } catch (error) {
      handleError(error);
    }
  }

  @Get(':id/users/:userId/roles')
  @ApiOperation({ summary: 'Get user roles in program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiParam({ name: 'userId', description: 'User UUID' })
  @ApiResponse({ status: 200, description: 'User roles retrieved successfully' })
  async getUserRoles(@Param('id', ParseUUIDPipe) programId: string, @Param('userId', ParseUUIDPipe) userId: string): Promise<{ roles: string[] }> {
    try {
      const roles = await this.programService.getUserRoles(programId, userId);
      return { roles };
    } catch (error) {
      handleError(error);
    }
  }

  // Statistics & Analytics
  @Get(':id/stats')
  @ApiOperation({ summary: 'Get program statistics' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 200, description: 'Program statistics retrieved successfully' })
  async getStats(@Param('id', ParseUUIDPipe) programId: string): Promise<any> {
    try {
      return await this.programService.getProgramStats(programId);
    } catch (error) {
      handleError(error);
    }
  }

  // Product Integration
  @Post(':id/products/:productId')
  @ApiOperation({ summary: 'Link program to product' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiParam({ name: 'productId', description: 'Product UUID' })
  @ApiResponse({ status: 201, description: 'Program linked to product successfully' })
  @ApiResponse({ status: 400, description: 'Link already exists' })
  async linkToProduct(
    @Param('id', ParseUUIDPipe) programId: string,
    @Param('productId', ParseUUIDPipe) productId: string,
    @Request() req: any,
  ): Promise<{ message: string; link: any }> {
    try {
      const userId = req.user?.id || 'mock-user-id';

      // Check permissions
      const hasPermission = await this.programService.checkUserPermission(programId, userId, [ProgramRoleType.INSTRUCTOR, ProgramRoleType.ADMINISTRATOR]);

      if (!hasPermission) {
        throw new HttpException('Insufficient permissions to link program to product', HttpStatus.FORBIDDEN);
      }

      const link = await this.programService.linkToProduct(programId, productId);
      return {
        message: 'Program linked to product successfully',
        link,
      };
    } catch (error) {
      handleError(error);
    }
  }

  @Delete(':id/products/:productId')
  @ApiOperation({ summary: 'Unlink program from product' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiParam({ name: 'productId', description: 'Product UUID' })
  @ApiResponse({ status: 200, description: 'Program unlinked from product successfully' })
  @ApiResponse({ status: 404, description: 'Link not found' })
  async unlinkFromProduct(
    @Param('id', ParseUUIDPipe) programId: string,
    @Param('productId', ParseUUIDPipe) productId: string,
    @Request() req: any,
  ): Promise<{ message: string }> {
    try {
      const userId = req.user?.id || 'mock-user-id';

      const hasPermission = await this.programService.checkUserPermission(programId, userId, [ProgramRoleType.INSTRUCTOR, ProgramRoleType.ADMINISTRATOR]);

      if (!hasPermission) {
        throw new HttpException('Insufficient permissions to unlink program from product', HttpStatus.FORBIDDEN);
      }

      await this.programService.unlinkFromProduct(programId, productId);
      return { message: 'Program unlinked from product successfully' };
    } catch (error) {
      handleError(error);
    }
  }

  // Search & Discovery
  @Get('search/:query')
  @ApiOperation({ summary: 'Search programs' })
  @ApiParam({ name: 'query', description: 'Search query' })
  @ApiQuery({ name: 'difficulty_level', required: false })
  @ApiQuery({ name: 'is_public', required: false })
  @ApiQuery({ name: 'tags', required: false })
  @ApiResponse({ status: 200, description: 'Search results retrieved successfully' })
  async search(@Param('query') query: string, @Query() filters: ProgramFiltersDto): Promise<Program[]> {
    try {
      if (typeof filters.tags === 'string') {
        filters.tags = (filters.tags as string).split(',').map((tag) => tag.trim());
      }

      return await this.programService.searchPrograms(query, filters);
    } catch (error) {
      handleError(error);
    }
  }

  @Get('recommendations/for-user')
  @ApiOperation({ summary: 'Get recommended programs for user' })
  @ApiQuery({ name: 'limit', required: false, description: 'Number of recommendations' })
  @ApiResponse({ status: 200, description: 'Recommendations retrieved successfully' })
  async getRecommendations(@Query('limit') limit?: number, @Request() req?: any): Promise<Program[]> {
    try {
      const userId = req?.user?.id || 'mock-user-id';
      const recommendationLimit = limit && limit > 0 ? Math.min(limit, 50) : 10;

      return await this.programService.getRecommendedPrograms(userId, recommendationLimit);
    } catch (error) {
      handleError(error);
    }
  }

  // Access Control
  @Get(':id/access-check')
  @ApiOperation({ summary: 'Check user access to program' })
  @ApiParam({ name: 'id', description: 'Program UUID' })
  @ApiResponse({ status: 200, description: 'Access check completed' })
  async checkAccess(@Param('id', ParseUUIDPipe) programId: string, @Request() req: any): Promise<{ has_access: boolean }> {
    try {
      const userId = req.user?.id || 'mock-user-id';
      const hasAccess = await this.programService.checkUserAccess(programId, userId);
      return { has_access: hasAccess };
    } catch (error) {
      handleError(error);
    }
  }
}
