import { Controller, Get, Inject } from '@nestjs/common';
import { ApiResponse } from '@nestjs/swagger';
import { Cache, CACHE_MANAGER } from '@nestjs/cache-manager';

@Controller('healthcheck')
export class HealthcheckController {
  constructor(@Inject(CACHE_MANAGER) private cacheManager: Cache) {}

  // db healthcheck
  @Get('db')
  @ApiResponse({ status: 200, description: 'DB is healthy' })
  @ApiResponse({ status: 500, description: 'DB is unhealthy' })
  async db(): Promise<void> {
    // check db health
    // throw error if db is unhealthy
  }

  // redis healthcheck
  @Get('redis')
  @ApiResponse({ status: 200, description: 'Redis is healthy' })
  @ApiResponse({ status: 500, description: 'Redis is unhealthy' })
  async redis(): Promise<void> {
    // check redis health
    // throw error if redis is unhealthy
  }

  // s3 healthcheck
  @Get('s3')
  @ApiResponse({ status: 200, description: 'S3 is healthy' })
  @ApiResponse({ status: 500, description: 'S3 is unhealthy' })
  async s3(): Promise<void> {
    // check s3 health
    // throw error if s3 is unhealthy
  }
}
