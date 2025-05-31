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

    let unusedColumns: { table_name: string, column_name: string }[] = [];
    // Step 4: Log all columns in existing tables that do not match the entity columns
    
    for (const entityMetadata of this.dataSource.entityMetadatas) {
      const tableName = entityMetadata.tableName;
      
      // Get all columns from the database for this table
      const dbColumns = await this.dataSource.query(
        `SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = $1`,
        [tableName]
      );
      
      // Get all column names defined in the entity (these should already be transformed by SnakeNamingStrategy)
      const entityColumnNames = entityMetadata.columns.map(column => column.databaseName);
      
      // Include foreign key columns from relations (these are also transformed by SnakeNamingStrategy)
      const relationColumnNames = entityMetadata.relations
        .filter(relation => relation.joinColumns && relation.joinColumns.length > 0)
        .flatMap(relation => relation.joinColumns.map(joinColumn => joinColumn.databaseName));
      
      // Include inverse side foreign key columns from many-to-one relations
      const inverseRelationColumns = entityMetadata.relations
        .filter(relation => relation.inverseJoinColumns && relation.inverseJoinColumns.length > 0)
        .flatMap(relation => relation.inverseJoinColumns.map(joinColumn => joinColumn.databaseName));
      
      // Include junction table columns for many-to-many relations
      const junctionRelationColumns = entityMetadata.relations
        .filter(relation => relation.junctionEntityMetadata)
        .flatMap(relation => {
          const junction = relation.junctionEntityMetadata;
          if (!junction) return [];
          
          return [
            ...junction.columns.map(col => col.databaseName),
            ...junction.foreignKeys.flatMap(fk => fk.columnNames),
          ];
        });
      
      // Combine all expected column names (all should be properly snake_cased)
      const allExpectedColumns = [
        ...entityColumnNames, 
        ...relationColumnNames, 
        ...inverseRelationColumns,
        ...junctionRelationColumns
      ].filter(Boolean); // Remove any undefined/null values
      
      // Find columns in database that are not in entity definition
      const orphanedColumns = dbColumns.filter((dbColumn: { column_name: string }) => 
        !allExpectedColumns.includes(dbColumn.column_name)
      );
      
      // Add orphaned columns to the list
      orphanedColumns.forEach((column: { column_name: string }) => {
        unusedColumns.push({
          table_name: tableName,
          column_name: column.column_name
        });
      });
    }

    if (unusedColumns.length > 0) {
      this.logger.warn('Unused Columns (exist in DB but not in entities):', unusedColumns);
    } else {
      this.logger.log('No unused columns found - all database columns match entity definitions');
    }
  }
}
