import { MigrationInterface, QueryRunner } from "typeorm";

export class AddJobApplicationWithdrawn1730336326869 implements MigrationInterface {
    name = 'AddJobApplicationWithdrawn1730336326869'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-application" ADD "withdrawn" boolean NOT NULL DEFAULT false`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "job-application" DROP COLUMN "withdrawn"`);
    }

}
