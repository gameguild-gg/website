import { Injectable, Logger } from '@nestjs/common';
import { DataSource } from 'typeorm';

@Injectable()
export class CleanupService {
  private logger = new Logger(CleanupService.name);

  constructor(private dataSource: DataSource) {}

  async onModuleInit() {
    // Step 1: Get all the tables in the database
    const tablesInDb = await this.dataSource.query("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

    // Step 2: Get all the entity names dynamically from the DataSource
    const entityNames = this.dataSource.entityMetadatas.map((metadata) => metadata.tableName);

    // Step 3: Identify unused tables and skip the 'migrations' table
    const unusedTables = tablesInDb.filter((table: { table_name: string }) => !entityNames.includes(table.table_name) && table.table_name !== 'migrations');

    if (unusedTables.length > 0) {
      this.logger.error('have you renamed or deleted any entities?');
      this.logger.warn('Unused Tables:', unusedTables);
    }
    return unusedTables;
  }
}
