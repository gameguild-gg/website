import { MigrationInterface, QueryRunner } from "typeorm";

export class AddLectureType1737400020093 implements MigrationInterface {
    name = 'AddLectureType1737400020093'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TYPE "public"."lecture_type_enum" AS ENUM('markdown', 'youtube', 'lexical', 'reveal', 'html', 'pdf', 'image', 'video', 'audio', 'code', 'link')`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "type" "public"."lecture_type_enum" NOT NULL DEFAULT 'markdown'`);
        await queryRunner.query(`CREATE INDEX "IDX_7f786286f5a809fad9f76466f6" ON "lecture" ("type") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_7f786286f5a809fad9f76466f6"`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "type"`);
        await queryRunner.query(`DROP TYPE "public"."lecture_type_enum"`);
    }

}
