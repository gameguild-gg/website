import { Injectable, Logger, OnModuleInit } from '@nestjs/common';
import { DataSource } from 'typeorm';
import * as fs from 'fs';

@Injectable()
export class SchemaDumpService implements OnModuleInit {
  private logger = new Logger(SchemaDumpService.name);

  constructor(private readonly dataSource: DataSource) {}

  async onModuleInit() {
    const queryRunner = this.dataSource.createQueryRunner();
    await queryRunner.connect();

    try {
      const schemaSQL = await this.getSchemaDump(queryRunner);
      fs.writeFileSync('schema_dump.sql', schemaSQL);
      this.logger.verbose('Database schema dumped to schema_dump.sql');
    } catch (error) {
      this.logger.error('Error generating schema dump:', error);
    } finally {
      await queryRunner.release();
    }
  }

  private async getSchemaDump(queryRunner): Promise<string> {
    let schemaSQL = '';

    // Step 1: Extract ENUM types
    const enumTypes = await queryRunner.query(`
        SELECT n.nspname                    AS schema,
               t.typname                    AS name,
               string_agg(e.enumlabel, ',') AS values
        FROM pg_type t
                 JOIN pg_enum e ON t.oid = e.enumtypid
                 JOIN pg_namespace n ON n.oid = t.typnamespace
        WHERE n.nspname = 'public'
        GROUP BY n.nspname, t.typname;
    `);

    for (const { name, values } of enumTypes) {
      schemaSQL += `CREATE TYPE "${name}" AS ENUM (${values
        .split(',')
        .map((v) => `'${v}'`)
        .join(', ')});\n\n`;
    }

    // Step 2: Extract table schemas
    const tables = await queryRunner.query(`
        SELECT tablename
        FROM pg_catalog.pg_tables
        WHERE schemaname = 'public';
    `);

    for (const { tablename } of tables) {
      const createTableResult = await queryRunner.query(
        `
            SELECT 'CREATE TABLE "' || table_name || '" (\n' ||
                   string_agg(
                           '  "' || column_name || '" ' ||
                           CASE
                               WHEN udt_name IN (SELECT typname FROM pg_type WHERE typtype = 'e')
                                   THEN '"' || udt_name || '"' -- Fix ENUM types
                               ELSE data_type
                               END ||
                           CASE
                               WHEN character_maximum_length IS NOT NULL
                                   THEN '(' || character_maximum_length || ')'
                               ELSE '' END ||
                           CASE WHEN is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END ||
                           CASE
                               WHEN column_default IS NOT NULL
                                   THEN ' DEFAULT ' || column_default
                               ELSE '' END,
                           ',\n'
                   ) ||
                   '\n);' AS create_statement
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = $1
            GROUP BY table_name;
        `,
        [tablename],
      );

      if (createTableResult.length > 0) {
        schemaSQL += createTableResult[0].create_statement + '\n\n';
      }

      // Step 3: Generate constraints (Primary Keys, Foreign Keys)
      const constraintsResult = await queryRunner.query(
        `
            SELECT conname AS constraint_name, pg_get_constraintdef(oid) AS constraint_def
            FROM pg_constraint
            WHERE conrelid::regclass::text = $1;
        `,
        [tablename],
      );

      for (const { constraint_def } of constraintsResult) {
        schemaSQL += `ALTER TABLE "${tablename}"
            ADD ${constraint_def};  `;
      }

      // Step 4: Generate indexes
      const indexesResult = await queryRunner.query(
        `
            SELECT indexdef
            FROM pg_indexes
            WHERE tablename = $1;
        `,
        [tablename],
      );

      for (const { indexdef } of indexesResult) {
        schemaSQL += `${indexdef};\n`;
      }

      schemaSQL += '\n';
    }

    return schemaSQL;
  }
}
