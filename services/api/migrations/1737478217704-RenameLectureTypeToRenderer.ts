import { MigrationInterface, QueryRunner } from "typeorm";

export class RenameLectureTypeToRenderer1737478217704 implements MigrationInterface {
    name = 'RenameLectureTypeToRenderer1737478217704'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_7f786286f5a809fad9f76466f6"`);
        await queryRunner.query(`ALTER TABLE "lecture" RENAME COLUMN "type" TO "renderer"`);
        await queryRunner.query(`ALTER TYPE "public"."lecture_type_enum" RENAME TO "lecture_renderer_enum"`);
        await queryRunner.query(`CREATE INDEX "IDX_d316a6a4d94b0b4aaab3af3f31" ON "lecture" ("renderer") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_d316a6a4d94b0b4aaab3af3f31"`);
        await queryRunner.query(`ALTER TYPE "public"."lecture_renderer_enum" RENAME TO "lecture_type_enum"`);
        await queryRunner.query(`ALTER TABLE "lecture" RENAME COLUMN "renderer" TO "type"`);
        await queryRunner.query(`CREATE INDEX "IDX_7f786286f5a809fad9f76466f6" ON "lecture" ("type") `);
    }

}
