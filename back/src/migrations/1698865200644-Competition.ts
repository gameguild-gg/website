import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1698865200644 implements MigrationInterface {
    name = 'Competition1698865200644'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_4a38724ebc328cadda60121090d"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" RENAME COLUMN "competition_run_id" TO "run_id"`);
        await queryRunner.query(`CREATE TABLE "competition_run_submission_report_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "wins_as_cat" integer NOT NULL, "wins_as_catcher" integer NOT NULL, "cat_points" double precision NOT NULL, "catcher_points" double precision NOT NULL, "total_points" double precision NOT NULL, "run_id" uuid, "submission_id" uuid, CONSTRAINT "PK_efb4ef524197954e35f131fb9ee" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD CONSTRAINT "FK_892a61c7a3d1d8915643c6cfcad" FOREIGN KEY ("run_id") REFERENCES "competition_run_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD CONSTRAINT "FK_9db155cb3344dfc07c41aa97356" FOREIGN KEY ("submission_id") REFERENCES "competition_submission_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_368ba6d197680cb80f377cac145" FOREIGN KEY ("run_id") REFERENCES "competition_run_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_368ba6d197680cb80f377cac145"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP CONSTRAINT "FK_9db155cb3344dfc07c41aa97356"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP CONSTRAINT "FK_892a61c7a3d1d8915643c6cfcad"`);
        await queryRunner.query(`DROP TABLE "competition_run_submission_report_entity"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" RENAME COLUMN "run_id" TO "competition_run_id"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_4a38724ebc328cadda60121090d" FOREIGN KEY ("competition_run_id") REFERENCES "competition_run_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
