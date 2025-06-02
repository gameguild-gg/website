import { MigrationInterface, QueryRunner } from 'typeorm';

export class DeleteTagTable1748138524745 implements MigrationInterface {
  name = 'DeleteTagTable1748138524745';

  public async up(queryRunner: QueryRunner): Promise<void> {
    // Drop the unused "tag" table
    await queryRunner.query(`DROP TABLE IF EXISTS "tag"`);
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    // Restore the "tag" table as it was originally defined
    await queryRunner.query(`CREATE TABLE "tag"
                             (
                                 "id"         uuid                        NOT NULL DEFAULT uuid_generate_v4(),
                                 "created_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "tag"        character varying(50)       NOT NULL DEFAULT ''
                             )`);
    
    // Restore primary key constraint
    await queryRunner.query(`ALTER TABLE "tag" ADD CONSTRAINT "PK_8e4052373c579afc1471f526760" PRIMARY KEY ("id")`);
    
    // Restore unique index on tag column
    await queryRunner.query(`CREATE UNIQUE INDEX "IDX_9dbf61b2d00d2c77d3b5ced37c" ON "tag" ("tag")`);
  }
}
