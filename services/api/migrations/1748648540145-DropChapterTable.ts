import { MigrationInterface, QueryRunner } from "typeorm";

export class DropChapterTable1748648540145 implements MigrationInterface {
    name = 'DropChapterTable1748648540145'

    public async up(queryRunner: QueryRunner): Promise<void> {
        // Drop foreign key constraints for chapter_editors_user table
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" DROP CONSTRAINT "FK_6bc76308984eee4bb9c49d9b2a7"`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" DROP CONSTRAINT "FK_22ec2de78ee0f0e2e7ee768a4c7"`);
        
        // Drop indexes for chapter_editors_user table
        await queryRunner.query(`DROP INDEX "IDX_6bc76308984eee4bb9c49d9b2a"`);
        await queryRunner.query(`DROP INDEX "IDX_22ec2de78ee0f0e2e7ee768a4c"`);
        
        // Drop chapter_editors_user table
        await queryRunner.query(`DROP TABLE "chapter_editors_user"`);
        
        // Drop foreign key constraints for chapter table
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_be4eebd798cc26bd6bded42f8c0"`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`);
        
        // Drop index for chapter table
        await queryRunner.query(`DROP INDEX "IDX_2c985baf03bef490c97e168cdc"`);
        
        // Drop chapter table
        await queryRunner.query(`DROP TABLE "chapter"`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        // Recreate chapter table
        await queryRunner.query(`CREATE TABLE "chapter" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "slug" character varying(255) DEFAULT '', "title" character varying(1024) DEFAULT '', "summary" character varying(1024) DEFAULT '', "body" jsonb DEFAULT '{}', "visibility" "public"."chapter_visibility_enum" NOT NULL DEFAULT 'DRAFT', "thumbnail" character varying DEFAULT '', "order" double precision NOT NULL DEFAULT '0', "owner_id" uuid, "course_id" uuid, CONSTRAINT "PK_275bd1c62bed7dff839680614ca" PRIMARY KEY ("id"))`);
        
        // Recreate chapter_editors_user table
        await queryRunner.query(`CREATE TABLE "chapter_editors_user" ("chapter_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_6b677152cc31755de3d795224f6" PRIMARY KEY ("chapter_id", "user_id"))`);
        
        // Recreate indexes
        await queryRunner.query(`CREATE INDEX "IDX_2c985baf03bef490c97e168cdc" ON "chapter" ("order")`);
        await queryRunner.query(`CREATE INDEX "IDX_6bc76308984eee4bb9c49d9b2a" ON "chapter_editors_user" ("chapter_id")`);
        await queryRunner.query(`CREATE INDEX "IDX_22ec2de78ee0f0e2e7ee768a4c" ON "chapter_editors_user" ("user_id")`);
        
        // Recreate foreign key constraints
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD CONSTRAINT "FK_be4eebd798cc26bd6bded42f8c0" FOREIGN KEY ("course_id") REFERENCES "course"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" ADD CONSTRAINT "FK_6bc76308984eee4bb9c49d9b2a7" FOREIGN KEY ("chapter_id") REFERENCES "chapter"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "chapter_editors_user" ADD CONSTRAINT "FK_22ec2de78ee0f0e2e7ee768a4c7" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }
}
