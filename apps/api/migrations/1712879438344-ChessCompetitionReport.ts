import { MigrationInterface, QueryRunner } from "typeorm";

export class ChessCompetitionReport1712879438344 implements MigrationInterface {
    name = 'ChessCompetitionReport1712879438344'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "p1_points"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "p2_points"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD "total_wins" integer NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD "points_as_p1" double precision NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD "points_as_p2" double precision NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD "user_id" uuid`);
        await queryRunner.query(`ALTER TABLE "competition_run_entity" ADD "game_type" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD CONSTRAINT "FK_23c154ce1a16cd0953144a790cc" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP CONSTRAINT "FK_23c154ce1a16cd0953144a790cc"`);
        await queryRunner.query(`ALTER TABLE "competition_run_entity" DROP COLUMN "game_type"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "user_id"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "points_as_p2"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "points_as_p1"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "total_wins"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD "p2_points" double precision NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD "p1_points" double precision NOT NULL`);
    }

}
