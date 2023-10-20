import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1697819210123 implements MigrationInterface {
    name = 'Competition1697819210123'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_run_entity" RENAME COLUMN "is_running" TO "state"`);
        await queryRunner.query(`ALTER TABLE "competition_run_entity" DROP COLUMN "state"`);
        await queryRunner.query(`ALTER TABLE "competition_run_entity" ADD "state" character varying NOT NULL DEFAULT 'NOT_STARTED'`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_run_entity" DROP COLUMN "state"`);
        await queryRunner.query(`ALTER TABLE "competition_run_entity" ADD "state" boolean NOT NULL DEFAULT false`);
        await queryRunner.query(`ALTER TABLE "competition_run_entity" RENAME COLUMN "state" TO "is_running"`);
    }

}
