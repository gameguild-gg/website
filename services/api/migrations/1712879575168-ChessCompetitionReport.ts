import { MigrationInterface, QueryRunner } from "typeorm";

export class ChessCompetitionReport1712879575168 implements MigrationInterface {
    name = 'ChessCompetitionReport1712879575168'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "wins_as_p1" SET DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "wins_as_p2" SET DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "total_wins" SET DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "points_as_p1" SET DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "points_as_p2" SET DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "total_points" SET DEFAULT '0'`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "total_points" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "points_as_p2" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "points_as_p1" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "total_wins" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "wins_as_p2" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ALTER COLUMN "wins_as_p1" DROP DEFAULT`);
    }

}
