import { Resolver, Query, Args, ID } from '@nestjs/graphql';
import { Int } from '@nestjs/graphql';
import { Program } from '../program/entities/program.entity';

@Resolver(() => Program)
export class ProgramResolver {
  // Note: Since this is a demo, we'll use mock data
  // In a real implementation, you would inject the ProgramService

  @Query(() => [Program], { name: 'programs' })
  async findAll(): Promise<Program[]> {
    // Mock data for demo - replace with actual service call
    return [];
  }

  @Query(() => Program, { name: 'program', nullable: true })
  async findOne(@Args('slug', { type: () => String }) slug: string): Promise<Program | null> {
    // Mock implementation - replace with actual service call
    return null;
  }

  @Query(() => Int, { name: 'programCount' })
  async programCount(): Promise<number> {
    // Mock implementation - replace with actual service call
    return 0;
  }
}
