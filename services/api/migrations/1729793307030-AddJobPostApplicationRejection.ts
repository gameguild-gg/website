import { MigrationInterface, QueryRunner } from "typeorm";

export class AddJobPostApplicationRejection1729793307030 implements MigrationInterface {
    name = 'AddJobPostApplicationRejection1729793307030'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-application" RENAME COLUMN "deleted_at" TO "rejected"`);
        await queryRunner.query(`ALTER TABLE "job-tag" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`ALTER TABLE "ticket" DROP COLUMN "deleted_at"`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "title" character varying(255) NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-post" ALTER COLUMN "summary" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-post" ALTER COLUMN "body" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-application" DROP COLUMN "rejected"`);
        await queryRunner.query(`ALTER TABLE "job-application" ADD "rejected" boolean NOT NULL DEFAULT false`);
        await queryRunner.query(`CREATE INDEX "IDX_244c4f211882ced89a917ec4f3" ON "job-post" ("title") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_244c4f211882ced89a917ec4f3"`);
        await queryRunner.query(`ALTER TABLE "job-application" DROP COLUMN "rejected"`);
        await queryRunner.query(`ALTER TABLE "job-application" ADD "rejected" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "job-post" ALTER COLUMN "body" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-post" ALTER COLUMN "summary" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-post" DROP COLUMN "title"`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "title" character varying(1024) NOT NULL`);
        await queryRunner.query(`ALTER TABLE "ticket" ADD "deleted_at" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "job-post" ADD "deleted_at" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "job-tag" ADD "deleted_at" TIMESTAMP`);
        await queryRunner.query(`ALTER TABLE "job-application" RENAME COLUMN "rejected" TO "deleted_at"`);
    }

}
