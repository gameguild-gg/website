import { MigrationInterface, QueryRunner } from "typeorm";

export class AddJobApplicationWithdrawn1730336326869 implements MigrationInterface {
    name = 'AddJobApplicationWithdrawn1730336326869'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-application" ADD "withdrawn" boolean NOT NULL`);
        await queryRunner.query(`ALTER TABLE "job-application" ALTER COLUMN "progress" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "job-application" ALTER COLUMN "rejected" DROP DEFAULT`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-application" ALTER COLUMN "rejected" SET DEFAULT false`);
        await queryRunner.query(`ALTER TABLE "job-application" ALTER COLUMN "progress" SET DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "job-application" DROP COLUMN "withdrawn"`);
    }

}
