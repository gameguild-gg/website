import { Injectable, NotFoundException, BadRequestException, ForbiddenException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, ILike, Or, IsNull } from 'typeorm';
import { Program, ProgramUser, ProgramUserRole, UserProduct } from '../../entities';
import { ProgramRoleType } from '../../entities/enums';
import { EnrollmentRequest, BulkEnrollmentRequest, EnrollmentFilters, EnrollmentStats } from './dtos';

@Injectable()
export class ProgramEnrollmentService {
  constructor(
    @InjectRepository(Program)
    private readonly programRepository: Repository<Program>,
    @InjectRepository(ProgramUser)
    private readonly enrollmentRepository: Repository<ProgramUser>,
    @InjectRepository(ProgramUserRole)
    private readonly roleRepository: Repository<ProgramUserRole>,
    @InjectRepository(UserProduct)
    private readonly userProductRepository: Repository<UserProduct>,
  ) {}

  // Core Enrollment Operations
  async enrollUser(enrollmentRequest: EnrollmentRequest): Promise<ProgramUser> {
    const { programId, userProductId, analytics, grades, progress } = enrollmentRequest;

    // Verify program exists
    const program = await this.programRepository.findOne({
      where: { id: programId },
    });

    if (!program) {
      throw new NotFoundException(`Program with ID ${programId} not found`);
    }

    // Verify user product exists
    const userProduct = await this.userProductRepository.findOne({
      where: { id: userProductId },
      relations: { user: true },
    });

    if (!userProduct) {
      throw new NotFoundException(`User product with ID ${userProductId} not found`);
    }

    // Check if user is already enrolled
    const existingEnrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (existingEnrollment && !existingEnrollment.deletedAt) {
      throw new BadRequestException('User is already enrolled in this program');
    }

    if (existingEnrollment && existingEnrollment.deletedAt) {
      // Re-activate enrollment
      existingEnrollment.deletedAt = null;
      existingEnrollment.analytics = analytics || {};
      existingEnrollment.grades = grades || {};
      existingEnrollment.progress = progress || {};
      return this.enrollmentRepository.save(existingEnrollment);
    }

    // Create new enrollment
    const enrollment = this.enrollmentRepository.create({
      program,
      userProduct,
      analytics: analytics || {},
      grades: grades || {},
      progress: progress || {},
    });

    const savedEnrollment = await this.enrollmentRepository.save(enrollment);

    // Assign default student role
    await this.assignRole(programId, userProductId, ProgramRoleType.STUDENT);

    return savedEnrollment;
  }

  async unenrollUser(programId: string, userProductId: string, reason?: string): Promise<void> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      throw new NotFoundException('User is not enrolled in this program');
    }

    // Soft delete the enrollment
    await this.enrollmentRepository.softDelete(enrollment.id);

    // Remove user roles
    const roles = await this.roleRepository.find({
      where: {
        program: { id: programId },
        programUser: { id: enrollment.id },
      },
    });

    for (const role of roles) {
      await this.roleRepository.softDelete(role.id);
    }
  }

  async bulkEnrollUsers(bulkRequest: BulkEnrollmentRequest): Promise<ProgramUser[]> {
    const { programId, userProductIds, defaultRole } = bulkRequest;
    const enrollments: ProgramUser[] = [];

    for (const userProductId of userProductIds) {
      try {
        const enrollment = await this.enrollUser({
          programId,
          userProductId,
        });
        enrollments.push(enrollment);

        // Assign specific role if different from default
        if (defaultRole && defaultRole !== ProgramRoleType.STUDENT) {
          await this.assignRole(programId, userProductId, defaultRole);
        }
      } catch (error) {
        // Log error but continue with other enrollments
        console.warn(`Failed to enroll user product ${userProductId}:`, error);
      }
    }

    return enrollments;
  }

  // Role Management
  async assignRole(programId: string, userProductId: string, role: ProgramRoleType): Promise<ProgramUserRole> {
    const program = await this.programRepository.findOne({
      where: { id: programId },
    });

    if (!program) {
      throw new NotFoundException('Program not found');
    }

    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      throw new NotFoundException('User enrollment not found');
    }

    // Check if role already exists
    const existingRole = await this.roleRepository.findOne({
      where: {
        program: { id: programId },
        programUser: { id: enrollment.id },
        role,
      },
    });

    if (existingRole) {
      if (existingRole.deletedAt) {
        existingRole.deletedAt = null;
        return this.roleRepository.save(existingRole);
      }
      return existingRole;
    }

    const userRole = this.roleRepository.create({
      program,
      programUser: enrollment,
      role,
    });

    return this.roleRepository.save(userRole);
  }

  async removeRole(programId: string, userProductId: string, role: ProgramRoleType): Promise<void> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      throw new NotFoundException('User enrollment not found');
    }

    const userRole = await this.roleRepository.findOne({
      where: {
        program: { id: programId },
        programUser: { id: enrollment.id },
        role,
      },
    });

    if (userRole) {
      await this.roleRepository.softDelete(userRole.id);
    }
  }

  // Enrollment Management
  async updateEnrollmentProgress(programId: string, userProductId: string, progressData: object): Promise<ProgramUser> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      throw new NotFoundException('User enrollment not found');
    }

    enrollment.progress = { ...enrollment.progress, ...progressData };
    return this.enrollmentRepository.save(enrollment);
  }

  async updateEnrollmentGrades(programId: string, userProductId: string, gradesData: object): Promise<ProgramUser> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      throw new NotFoundException('User enrollment not found');
    }

    enrollment.grades = { ...enrollment.grades, ...gradesData };
    return this.enrollmentRepository.save(enrollment);
  }

  // Query Operations
  async getEnrollments(filters: EnrollmentFilters): Promise<ProgramUser[]> {
    // Use type-safe find options instead of query builder with raw strings
    const whereConditions: any = {
      deletedAt: IsNull(),
    };

    if (filters.programId) {
      whereConditions.program = { id: filters.programId };
    }

    if (filters.userId) {
      whereConditions.userProduct = { user: { id: filters.userId } };
    }

    // For search functionality, use type-safe repository queries with OR conditions
    if (filters.search) {
      const searchPattern = `%${filters.search}%`;
      
      // Use repository find with type-safe OR conditions for search
      const searchResults = await this.enrollmentRepository.find({
        where: [
          // Search by program slug
          {
            deletedAt: IsNull(),
            program: { slug: ILike(searchPattern) },
          },
          // Search by user username
          {
            deletedAt: IsNull(),
            userProduct: { user: { username: ILike(searchPattern) } },
          },
          // Search by user email
          {
            deletedAt: IsNull(),
            userProduct: { user: { email: ILike(searchPattern) } },
          },
        ],
        relations: {
          program: true,
          userProduct: {
            user: true,
          },
        },
        take: filters.limit,
        skip: filters.offset,
      });

      return searchResults;
    }

    return this.enrollmentRepository.find({
      where: whereConditions,
      relations: {
        program: true,
        userProduct: {
          user: true,
        },
      },
      take: filters.limit,
      skip: filters.offset,
    });
  }

  async getEnrollmentById(enrollmentId: string): Promise<ProgramUser> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: { id: enrollmentId },
      relations: { program: true, userProduct: { user: true } },
    });

    if (!enrollment) {
      throw new NotFoundException('Enrollment not found');
    }

    return enrollment;
  }

  async getUserEnrollments(userId: string): Promise<ProgramUser[]> {
    return this.enrollmentRepository.find({
      where: {
        userProduct: { user: { id: userId } } as any,
      },
      relations: { program: true, userProduct: true },
    });
  }

  async getProgramEnrollments(programId: string): Promise<ProgramUser[]> {
    return this.enrollmentRepository.find({
      where: {
        program: { id: programId },
      },
      relations: { userProduct: { user: true } },
    });
  }

  // Statistics
  async getEnrollmentStats(programId: string): Promise<EnrollmentStats> {
    const total_enrollments = await this.enrollmentRepository.count({
      where: { program: { id: programId } },
    });

    const active_enrollments = await this.enrollmentRepository.count({
      where: {
        program: { id: programId },
        deletedAt: null,
      },
    });

    // Note: Completion tracking would need to be calculated from content interactions
    // For now, returning simplified stats

    const completed = 0; // Would calculate from content completion
    const in_progress = 0; // Would calculate from progress data  
    const not_started = 0; // Would calculate from enrollment without progress
    const completion_rate = 0; // Would calculate from content completion
    const average_progress = 0; // Would calculate from progress data
    const dropout_rate = total_enrollments > 0 ? ((total_enrollments - active_enrollments) / total_enrollments) * 100 : 0;

    return {
      total_enrollments,
      active_enrollments,
      completed,
      in_progress,
      not_started,
      completion_rate,
      average_progress,
      dropout_rate,
    };
  }

  // Utility methods
  async checkEnrollmentExists(programId: string, userProductId: string): Promise<boolean> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    return !!enrollment && !enrollment.deletedAt;
  }

  async getUserRoles(programId: string, userProductId: string): Promise<ProgramRoleType[]> {
    const enrollment = await this.enrollmentRepository.findOne({
      where: {
        program: { id: programId },
        userProduct: { id: userProductId },
      },
    });

    if (!enrollment) {
      return [];
    }

    const roles = await this.roleRepository.find({
      where: {
        programUser: { id: enrollment.id },
      },
      select: ['role'],
    });

    return roles.map((r) => r.role);
  }
}
