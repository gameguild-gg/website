import { BadRequestException, ForbiddenException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, ILike, Or, IsNull, ArrayContains } from 'typeorm';
import { Product, ProductProgram, Program, ProgramFeedbackSubmission, ProgramRatingEntity, ProgramUser, ProgramUserRole, UserProduct } from '../entities';
import { CreateProgramDto, ProgramFilters, ProgramStats, UpdateProgramDto } from './dtos';
import { ProgramRoleType } from '../entities/enums';

@Injectable()
export class ProgramService {
  constructor(
    @InjectRepository(Program)
    private readonly programRepository: Repository<Program>,
    @InjectRepository(ProgramUser)
    private readonly programUserRepository: Repository<ProgramUser>,
    @InjectRepository(ProgramUserRole)
    private readonly programUserRoleRepository: Repository<ProgramUserRole>,
    @InjectRepository(ProductProgram)
    private readonly productProgramRepository: Repository<ProductProgram>,
    @InjectRepository(ProgramRatingEntity)
    private readonly programRatingRepository: Repository<ProgramRatingEntity>,
    @InjectRepository(ProgramFeedbackSubmission)
    private readonly feedbackRepository: Repository<ProgramFeedbackSubmission>,
    @InjectRepository(UserProduct)
    private readonly userProductRepository: Repository<UserProduct>,
  ) {}

  // Core CRUD Operations
  async create(createProgramDto: CreateProgramDto, creatorId: string): Promise<Program> {
    const program = this.programRepository.create({
      ...createProgramDto,
      cachedEnrollmentCount: 0,
      cachedCompletionCount: 0,
      cachedStudentsCompletionRate: 0.0,
      cachedRating: 0.0,
    });

    const savedProgram = await this.programRepository.save(program);

    // Automatically assign creator as instructor (assuming we have their UserProduct)
    const userProduct = await this.userProductRepository.findOne({
      where: { user: { id: creatorId } as any },
    });

    if (userProduct) {
      await this.assignUserRole(savedProgram.id, userProduct.id, ProgramRoleType.INSTRUCTOR);
    }

    return savedProgram;
  }

  async findAll(filters?: ProgramFilters): Promise<Program[]> {
    // Use type-safe find options instead of raw string queries
    const whereConditions: any = {
      deletedAt: IsNull(),
    };

    // For search functionality, we need special handling due to OR conditions
    if (filters?.search) {
      const searchPattern = `%${filters.search}%`;
      whereConditions.slug = ILike(searchPattern);
      const alternativeWhere = {
        deletedAt: IsNull(),
        summary: ILike(searchPattern),
      };
      
      // Use type-safe repository find with OR conditions for search  
      const searchWhereConditions = [
        // Search by program slug
        {
          deletedAt: IsNull(),
          slug: ILike(searchPattern),
          ...(filters?.tenancyDomains?.length && { tenancyDomains: ArrayContains(filters.tenancyDomains) }),
        },
        // Search by program summary
        {
          deletedAt: IsNull(), 
          summary: ILike(searchPattern),
          ...(filters?.tenancyDomains?.length && { tenancyDomains: ArrayContains(filters.tenancyDomains) }),
        },
      ];
      
      return this.programRepository.find({
        where: searchWhereConditions,
      });
    }

    if (filters?.tenancyDomains?.length) {
      whereConditions.tenancyDomains = ArrayContains(filters.tenancyDomains);
    }

    return this.programRepository.find({
      where: whereConditions,
    });
  }

  async findOne(id: string): Promise<Program> {
    const program = await this.programRepository.findOne({
      where: { id },
      relations: { programUsers: true, contents: true },
    });

    if (!program) {
      throw new NotFoundException(`Program with ID ${id} not found`);
    }

    return program;
  }

  async update(id: string, updateProgramDto: UpdateProgramDto, userId: string): Promise<Program> {
    const program = await this.findOne(id);

    // Check if user has permission to update
    const hasPermission = await this.checkUserPermission(id, userId, [ProgramRoleType.INSTRUCTOR, ProgramRoleType.ADMINISTRATOR]);
    if (!hasPermission) {
      throw new ForbiddenException('You do not have permission to update this program');
    }

    Object.assign(program, updateProgramDto);

    return this.programRepository.save(program);
  }

  async remove(id: string, userId: string): Promise<void> {
    const program = await this.findOne(id);

    // Check if user has permission to delete
    const hasPermission = await this.checkUserPermission(id, userId, [ProgramRoleType.INSTRUCTOR, ProgramRoleType.ADMINISTRATOR]);
    if (!hasPermission) {
      throw new ForbiddenException('You do not have permission to delete this program');
    }

    // Soft delete using built-in soft delete functionality
    await this.programRepository.softDelete(id);
  }

  // Enrollment Management
  async enrollUser(userProductId: string, programId: string): Promise<ProgramUser> {
    const program = await this.findOne(programId);

    const userProduct = await this.userProductRepository.findOne({
      where: { id: userProductId },
      relations: { user: true },
    });

    if (!userProduct) {
      throw new NotFoundException('User product not found');
    }

    // Check if user is already enrolled
    const existingEnrollment = await this.programUserRepository.findOne({
      where: {
        userProduct: { id: userProductId },
        program: { id: programId },
      },
    });

    if (existingEnrollment && !existingEnrollment.deletedAt) {
      throw new BadRequestException('User is already enrolled in this program');
    }

    if (existingEnrollment && existingEnrollment.deletedAt) {
      // Re-activate enrollment by removing soft delete
      existingEnrollment.deletedAt = null;
      return this.programUserRepository.save(existingEnrollment);
    }

    const enrollment = this.programUserRepository.create({
      userProduct,
      program,
      analytics: {},
      grades: {},
      progress: {},
    });

    const savedEnrollment = await this.programUserRepository.save(enrollment);

    // Assign default student role
    await this.assignUserRole(programId, userProductId, ProgramRoleType.STUDENT);

    return savedEnrollment;
  }

  async unenrollUser(programId: string, userProductId: string): Promise<void> {
    const enrollment = await this.programUserRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      throw new NotFoundException('User is not enrolled in this program');
    }

    // Soft delete the enrollment
    await this.programUserRepository.softDelete(enrollment.id);

    // Remove user roles
    const roles = await this.programUserRoleRepository.find({
      where: {
        program: { id: programId },
        programUser: { id: enrollment.id },
      },
    });

    for (const role of roles) {
      await this.programUserRoleRepository.softDelete(role.id);
    }
  }

  // Role Management
  async assignUserRole(programId: string, userProductId: string, role: ProgramRoleType): Promise<ProgramUserRole> {
    const program = await this.findOne(programId);

    const programUser = await this.programUserRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!programUser) {
      throw new NotFoundException('User enrollment not found');
    }

    // Check if role already exists
    const existingRole = await this.programUserRoleRepository.findOne({
      where: {
        program: { id: programId },
        programUser: { id: programUser.id },
        role,
      },
    });

    if (existingRole) {
      if (existingRole.deletedAt) {
        existingRole.deletedAt = null;
        return this.programUserRoleRepository.save(existingRole);
      }
      return existingRole;
    }

    const userRole = this.programUserRoleRepository.create({
      program,
      programUser,
      role,
    });

    return this.programUserRoleRepository.save(userRole);
  }

  async removeUserRole(programId: string, userProductId: string, role: ProgramRoleType): Promise<void> {
    const programUser = await this.programUserRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!programUser) {
      throw new NotFoundException('User enrollment not found');
    }

    const userRole = await this.programUserRoleRepository.findOne({
      where: {
        program: { id: programId },
        programUser: { id: programUser.id },
        role,
      },
    });

    if (userRole) {
      await this.programUserRoleRepository.softDelete(userRole.id);
    }
  }

  async getUserRoles(programId: string, userId: string): Promise<ProgramRoleType[]> {
    const programUser = await this.programUserRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { user: { id: userId } } as any,
      },
      relations: { roles: true },
    });

    if (!programUser) {
      return [];
    }

    const roles = await this.programUserRoleRepository.find({
      where: {
        programUser: { id: programUser.id },
      },
      select: ['role'],
    });

    return roles.map((r) => r.role);
  }

  // Permission & Access Control
  async checkUserPermission(programId: string, userId: string, requiredRoles: ProgramRoleType[]): Promise<boolean> {
    const userRoles = await this.getUserRoles(programId, userId);
    return requiredRoles.some((role) => userRoles.includes(role));
  }

  async checkUserAccess(programId: string, userId: string): Promise<boolean> {
    // Check if user is enrolled
    const enrollment = await this.programUserRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { user: { id: userId } } as any,
      },
    });

    return !!enrollment;
  }

  // Statistics & Analytics
  async getProgramStats(programId: string): Promise<ProgramStats> {
    const enrollments = await this.programUserRepository.count({
      where: { program: { id: programId } },
    });

    // Note: The actual Program and ProgramUser entities don't have completion_status fields
    // This would need to be calculated from ContentInteraction entities
    // For now, returning placeholder values

    return {
      total_enrollments: enrollments,
      completion_rate: 0, // Would need to calculate from content interactions
      average_rating: 0, // Would need to calculate from ratings
      total_ratings: 0,
      active_participants: enrollments, // Simplified - all enrolled are considered active
    };
  }

  // Product Integration
  async linkToProduct(programId: string, productId: string): Promise<ProductProgram> {
    const existingLink = await this.productProgramRepository.findOne({
      where: {
        program: { id: programId },
        product: { id: productId },
      },
    });

    if (existingLink) {
      throw new BadRequestException('Program is already linked to this product');
    }

    const program = await this.findOne(programId);
    const product = await this.productProgramRepository.manager.findOne(Product, {
      where: { id: productId },
    });

    if (!product) {
      throw new NotFoundException('Product not found');
    }

    const link = this.productProgramRepository.create({
      program,
      product,
      orderIndex: 0,
      isPrimary: false,
    });

    return this.productProgramRepository.save(link);
  }

  async unlinkFromProduct(programId: string, productId: string): Promise<void> {
    const link = await this.productProgramRepository.findOne({
      where: {
        program: { id: programId },
        product: { id: productId },
      },
    });

    if (!link) {
      throw new NotFoundException('Program-product link not found');
    }

    await this.productProgramRepository.remove(link);
  }

  // Search & Discovery
  async searchPrograms(query: string, filters?: ProgramFilters): Promise<Program[]> {
    return this.findAll({
      ...filters,
      search: query,
    });
  }

  async getRecommendedPrograms(userId: string, limit: number = 10): Promise<Program[]> {
    // Simple recommendation based on user's enrolled programs
    // This is a simplified implementation
    const userEnrollments = await this.programUserRepository.find({
      where: {
        userProduct: { user: { id: userId } } as any,
      },
      relations: { program: true },
      take: limit,
    });

    if (userEnrollments.length === 0) {
      // Return recent programs for new users
      return this.programRepository.find({
        take: limit,
        order: { createdAt: 'DESC' },
      });
    }

    // For now, just return all programs (this would be enhanced with ML)
    return this.programRepository.find({
      take: limit,
      order: { cachedRating: 'DESC' },
    });
  }
}
