import { Injectable, NotFoundException, BadRequestException, ForbiddenException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, MoreThan, Equal, IsNull, MoreThanOrEqual, LessThanOrEqual } from 'typeorm';
import { ProgramContent, ContentInteraction, ActivityGrade, Program, ProgramUser, UserProduct } from '../../entities';
import { ProgramContentType, ProgressStatus } from '../../entities/enums';
import { CreateContentDto, UpdateContentDto, ContentInteractionDto, ContentFilters, ContentProgress } from './dtos';

@Injectable()
export class ProgramContentService {
  constructor(
    @InjectRepository(ProgramContent)
    private readonly contentRepository: Repository<ProgramContent>,
    @InjectRepository(ContentInteraction)
    private readonly interactionRepository: Repository<ContentInteraction>,
    @InjectRepository(ActivityGrade)
    private readonly gradeRepository: Repository<ActivityGrade>,
    @InjectRepository(Program)
    private readonly programRepository: Repository<Program>,
    @InjectRepository(ProgramUser)
    private readonly programUserRepository: Repository<ProgramUser>,
  ) {}

  // Core CRUD Operations
  async create(createContentDto: CreateContentDto, creatorId: string): Promise<ProgramContent> {
    // Verify program exists and user has permission
    const program = await this.programRepository.findOne({
      where: { id: createContentDto.programId },
    });

    if (!program) {
      throw new NotFoundException(`Program with ID ${createContentDto.programId} not found`);
    }

    // Auto-generate order if not provided
    let order = createContentDto.order;
    if (order === undefined) {
      const lastContent = await this.contentRepository.findOne({
        where: { program: { id: createContentDto.programId } },
        order: { order: 'DESC' },
      });
      order = (lastContent?.order || 0) + 1;
    }

    // Handle parent relationship
    let parent: ProgramContent | null = null;
    if (createContentDto.parentId) {
      parent = await this.contentRepository.findOne({
        where: { id: createContentDto.parentId },
      });
      if (!parent) {
        throw new NotFoundException(`Parent content with ID ${createContentDto.parentId} not found`);
      }
    }

    const content = this.contentRepository.create({
      program: { id: createContentDto.programId },
      parent,
      type: createContentDto.type,
      title: createContentDto.title,
      summary: createContentDto.summary || null,
      order,
      body: createContentDto.body,
      previewable: createContentDto.previewable || false,
      dueDate: createContentDto.dueDate || null,
      availableFrom: createContentDto.availableFrom || null,
      availableTo: createContentDto.availableTo || null,
      gradingMethod: createContentDto.gradingMethod || null,
      durationMinutes: createContentDto.durationMinutes || null,
      textResponse: createContentDto.textResponse || false,
      urlResponse: createContentDto.urlResponse || false,
      fileResponseExtensions: createContentDto.fileResponseExtensions || null,
      gradingRubric: createContentDto.gradingRubric || null,
      metadata: createContentDto.metadata || null,
    });

    return this.contentRepository.save(content);
  }

  async findAll(filters?: ContentFilters): Promise<ProgramContent[]> {
    // Use typed find options instead of query builder with raw strings
    const whereConditions: any = {
      deletedAt: IsNull(),
    };

    if (filters?.programId) {
      whereConditions.program = { id: filters.programId };
    }

    if (filters?.parentId) {
      whereConditions.parent = { id: filters.parentId };
    }

    if (filters?.type) {
      whereConditions.type = Equal(filters.type);
    }

    if (filters?.previewable !== undefined) {
      whereConditions.previewable = Equal(filters.previewable);
    }

    if (filters?.availableFrom) {
      whereConditions.availableFrom = MoreThanOrEqual(filters.availableFrom);
    }

    if (filters?.availableTo) {
      whereConditions.availableTo = LessThanOrEqual(filters.availableTo);
    }

    return this.contentRepository.find({
      where: whereConditions,
      relations: {
        program: true,
        parent: true,
      },
      order: {
        order: 'ASC',
      },
    });
  }

  async findOne(id: string): Promise<ProgramContent> {
    const content = await this.contentRepository.findOne({
      where: { id, deletedAt: null },
      relations: { program: true, parent: true, interactions: true, children: true },
    });

    if (!content) {
      throw new NotFoundException(`Content with ID ${id} not found`);
    }

    return content;
  }

  async update(id: string, updateContentDto: UpdateContentDto): Promise<ProgramContent> {
    const content = await this.findOne(id);

    // Handle parent relationship update
    if (updateContentDto.parentId !== undefined) {
      if (updateContentDto.parentId === null) {
        content.parent = null;
      } else {
        const parent = await this.contentRepository.findOne({
          where: { id: updateContentDto.parentId },
        });
        if (!parent) {
          throw new NotFoundException(`Parent content with ID ${updateContentDto.parentId} not found`);
        }
        content.parent = parent;
      }
    }

    // Update other fields
    Object.assign(content, {
      type: updateContentDto.type || content.type,
      title: updateContentDto.title || content.title,
      summary: updateContentDto.summary !== undefined ? updateContentDto.summary : content.summary,
      order: updateContentDto.order !== undefined ? updateContentDto.order : content.order,
      body: updateContentDto.body || content.body,
      previewable: updateContentDto.previewable !== undefined ? updateContentDto.previewable : content.previewable,
      dueDate: updateContentDto.dueDate !== undefined ? updateContentDto.dueDate : content.dueDate,
      availableFrom: updateContentDto.availableFrom !== undefined ? updateContentDto.availableFrom : content.availableFrom,
      availableTo: updateContentDto.availableTo !== undefined ? updateContentDto.availableTo : content.availableTo,
      gradingMethod: updateContentDto.gradingMethod !== undefined ? updateContentDto.gradingMethod : content.gradingMethod,
      durationMinutes: updateContentDto.durationMinutes !== undefined ? updateContentDto.durationMinutes : content.durationMinutes,
      textResponse: updateContentDto.textResponse !== undefined ? updateContentDto.textResponse : content.textResponse,
      urlResponse: updateContentDto.urlResponse !== undefined ? updateContentDto.urlResponse : content.urlResponse,
      fileResponseExtensions: updateContentDto.fileResponseExtensions !== undefined ? updateContentDto.fileResponseExtensions : content.fileResponseExtensions,
      gradingRubric: updateContentDto.gradingRubric !== undefined ? updateContentDto.gradingRubric : content.gradingRubric,
      metadata: updateContentDto.metadata !== undefined ? updateContentDto.metadata : content.metadata,
    });

    return this.contentRepository.save(content);
  }

  async remove(id: string): Promise<void> {
    const content = await this.findOne(id);

    // Soft delete using TypeORM's built-in soft delete
    await this.contentRepository.softDelete(id);
  }

  // Content Interaction Management
  async recordInteraction(interactionDto: ContentInteractionDto): Promise<ContentInteraction> {
    const content = await this.findOne(interactionDto.contentId);

    // Get the program user enrollment
    const programUser = await this.programUserRepository.findOne({
      where: { id: interactionDto.programUserId },
    });

    if (!programUser) {
      throw new NotFoundException(`Program user enrollment with ID ${interactionDto.programUserId} not found`);
    }

    // Check if interaction already exists for this user/content
    const existingInteraction = await this.interactionRepository.findOne({
      where: {
        programUser: { id: interactionDto.programUserId },
        content: { id: interactionDto.contentId },
      },
    });

    if (existingInteraction) {
      // Update existing interaction
      Object.assign(existingInteraction, {
        status: interactionDto.status,
        startedAt: interactionDto.startedAt || existingInteraction.startedAt,
        completedAt: interactionDto.completedAt || existingInteraction.completedAt,
        timeSpentSeconds: interactionDto.timeSpentSeconds || existingInteraction.timeSpentSeconds,
        lastAccessedAt: new Date(),
        submittedAt: interactionDto.submittedAt || existingInteraction.submittedAt,
        answers: interactionDto.answers || existingInteraction.answers,
        textResponse: interactionDto.textResponse || existingInteraction.textResponse,
        urlResponse: interactionDto.urlResponse || existingInteraction.urlResponse,
        fileResponse: interactionDto.fileResponse || existingInteraction.fileResponse,
        metadata: interactionDto.metadata || existingInteraction.metadata,
      });
      return this.interactionRepository.save(existingInteraction);
    }

    // Create new interaction
    const interaction = this.interactionRepository.create({
      programUser: { id: interactionDto.programUserId },
      content: { id: interactionDto.contentId },
      status: interactionDto.status,
      startedAt: interactionDto.startedAt || null,
      completedAt: interactionDto.completedAt || null,
      timeSpentSeconds: interactionDto.timeSpentSeconds || 0,
      lastAccessedAt: new Date(),
      submittedAt: interactionDto.submittedAt || null,
      answers: interactionDto.answers || null,
      textResponse: interactionDto.textResponse || null,
      urlResponse: interactionDto.urlResponse || null,
      fileResponse: interactionDto.fileResponse || null,
      metadata: interactionDto.metadata || null,
    });

    return this.interactionRepository.save(interaction);
  }

  async getUserContentProgress(programUserId: string, programId: string): Promise<ContentProgress[]> {
    const contents = await this.findAll({
      programId: programId,
    });

    const progressPromises = contents.map(async (content) => {
      const interaction = await this.interactionRepository.findOne({
        where: {
          programUser: { id: programUserId },
          content: { id: content.id },
        },
      });

      return {
        contentId: content.id,
        title: content.title,
        type: content.type,
        status: interaction?.status || ProgressStatus.NOT_STARTED,
        timeSpentSeconds: interaction?.timeSpentSeconds || 0,
        startedAt: interaction?.startedAt || null,
        completedAt: interaction?.completedAt || null,
        lastAccessedAt: interaction?.lastAccessedAt || null,
        durationMinutes: content.durationMinutes || null,
      };
    });

    return Promise.all(progressPromises);
  }

  async getContentStatistics(contentId: string): Promise<any> {
    const [totalInteractions, completedInteractions] = await Promise.all([
      this.interactionRepository.count({
        where: { content: { id: contentId } },
      }),
      this.interactionRepository.count({
        where: {
          content: { id: contentId },
          status: ProgressStatus.COMPLETED,
        },
      }),
    ]);

    // Use relation-based filtering for better type safety in aggregation queries
    const averageTimeSpent = await this.interactionRepository
      .createQueryBuilder('interaction')
      .select('AVG(interaction.timeSpentSeconds)', 'avg_time')
      .where('interaction.contentId = :contentId', { contentId })
      .getRawOne();

    const averageGrade = await this.gradeRepository
      .createQueryBuilder('grade')
      .select('AVG(grade.pointsEarned)', 'avg_grade')
      .innerJoin('grade.contentInteraction', 'interaction')
      .where('interaction.contentId = :contentId', { contentId })
      .getRawOne();

    return {
      total_interactions: totalInteractions,
      total_completions: completedInteractions,
      completion_rate: totalInteractions > 0 ? (completedInteractions / totalInteractions) * 100 : 0,
      average_time_spent_seconds: parseFloat(averageTimeSpent?.avg_time || '0'),
      average_grade: parseFloat(averageGrade?.avg_grade || '0'),
    };
  }

  // Content Ordering and Structure
  async reorderContent(programId: string, contentOrders: { id: string; position: number }[]): Promise<void> {
    const updatePromises = contentOrders.map(({ id, position }) =>
      this.contentRepository.update(id, {
        order: position,
      }),
    );

    await Promise.all(updatePromises);
  }

  async getContentSequence(programId: string, programUserId?: string): Promise<any[]> {
    const contents = await this.findAll({
      programId: programId,
    });

    if (!programUserId) {
      return contents;
    }

    // Add progress information for user
    const contentsWithProgress = await Promise.all(
      contents.map(async (content) => {
        const interaction = await this.interactionRepository.findOne({
          where: {
            programUser: { id: programUserId },
            content: { id: content.id },
          },
        });

        return {
          ...content,
          userStatus: interaction?.status || ProgressStatus.NOT_STARTED,
          userTimeSpent: interaction?.timeSpentSeconds || 0,
          lastAccessed: interaction?.lastAccessedAt || null,
        };
      }),
    );

    return contentsWithProgress;
  }

  // Content Access Control
  async checkContentAccess(contentId: string, programUserId: string): Promise<boolean> {
    const content = await this.findOne(contentId);

    // Check if content is available based on dates
    const now = new Date();
    if (content.availableFrom && content.availableFrom > now) {
      return false;
    }

    if (content.availableTo && content.availableTo < now) {
      return false;
    }

    // For now, we'll assume access is granted if the user is enrolled
    // In a more complex system, you might check prerequisites here
    return true;
  }

  async getNextContent(currentContentId: string, programUserId: string): Promise<ProgramContent | null> {
    const currentContent = await this.findOne(currentContentId);

    const nextContent = await this.contentRepository.findOne({
      where: {
        program: { id: currentContent.program.id },
        order: MoreThan(currentContent.order),
        deletedAt: null,
      },
      order: { order: 'ASC' },
    });

    if (!nextContent) {
      return null;
    }

    // Check if user has access to next content
    const hasAccess = await this.checkContentAccess(nextContent.id, programUserId);

    return hasAccess ? nextContent : null;
  }

  // Content Analytics
  async getDetailedContentAnalytics(contentId: string): Promise<any> {
    const interactions = await this.interactionRepository.find({
      where: { content: { id: contentId } },
      relations: { programUser: true },
    });

    const interactionsByStatus = interactions.reduce(
      (acc, interaction) => {
        if (!acc[interaction.status]) {
          acc[interaction.status] = [];
        }
        acc[interaction.status].push(interaction);
        return acc;
      },
      {} as Record<string, ContentInteraction[]>,
    );

    const uniqueUsers = new Set(interactions.map((i) => i.programUser.id)).size;

    const averageTimeToComplete = await this.calculateAverageCompletionTime(contentId);

    return {
      unique_users: uniqueUsers,
      interactions_by_status: Object.keys(interactionsByStatus).map((status) => ({
        status,
        count: interactionsByStatus[status].length,
      })),
      average_completion_time_seconds: averageTimeToComplete,
      engagement_timeline: this.generateEngagementTimeline(interactions),
    };
  }

  private async calculateAverageCompletionTime(contentId: string): Promise<number> {
    const completedInteractions = await this.interactionRepository.find({
      where: {
        content: { id: contentId },
        status: ProgressStatus.COMPLETED,
        startedAt: { not: null } as any,
        completedAt: { not: null } as any,
      },
    });

    if (completedInteractions.length === 0) return 0;

    const completionTimes = completedInteractions
      .filter((interaction) => interaction.startedAt && interaction.completedAt)
      .map((interaction) => {
        const timeDiff = interaction.completedAt!.getTime() - interaction.startedAt!.getTime();
        return timeDiff / 1000; // Convert to seconds
      })
      .filter((time) => time > 0);

    return completionTimes.length > 0 ? completionTimes.reduce((sum, time) => sum + time, 0) / completionTimes.length : 0;
  }

  private generateEngagementTimeline(interactions: ContentInteraction[]): any[] {
    const timelineData = interactions.reduce(
      (acc, interaction) => {
        if (!interaction.lastAccessedAt) return acc;

        const dateKey = interaction.lastAccessedAt.toISOString().split('T')[0];
        if (!acc[dateKey]) {
          acc[dateKey] = {
            date: dateKey,
            total_interactions: 0,
            started: 0,
            completed: 0,
          };
        }

        acc[dateKey].total_interactions++;

        if (interaction.status === ProgressStatus.IN_PROGRESS) {
          acc[dateKey].started++;
        }

        if (interaction.status === ProgressStatus.COMPLETED) {
          acc[dateKey].completed++;
        }

        return acc;
      },
      {} as Record<string, any>,
    );

    return Object.values(timelineData).sort((a: any, b: any) => new Date(a.date).getTime() - new Date(b.date).getTime());
  }
}
