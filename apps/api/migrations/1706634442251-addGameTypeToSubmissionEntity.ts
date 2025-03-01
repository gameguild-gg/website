import { MigrationInterface, QueryRunner } from "typeorm";

export class AddGameTypeToSubmissionEntity1706634442251 implements MigrationInterface {
    name = 'AddGameTypeToSubmissionEntity1706634442251'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "competition_run_submission_report_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "wins_as_p1" integer NOT NULL, "wins_as_p2" integer NOT NULL, "p1_points" double precision NOT NULL, "p2_points" double precision NOT NULL, "total_points" double precision NOT NULL, "run_id" uuid, "submission_id" uuid, CONSTRAINT "PK_efb4ef524197954e35f131fb9ee" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "competition_run_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "state" character varying NOT NULL DEFAULT 'NOT_STARTED', CONSTRAINT "PK_c2c36337de9af6a41752324bc01" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TYPE "public"."competition_match_entity_winner_enum" AS ENUM('Player1', 'Player2')`);
        await queryRunner.query(`CREATE TABLE "competition_match_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "winner" "public"."competition_match_entity_winner_enum", "p1_points" double precision NOT NULL, "p2_points" double precision NOT NULL, "p1_turns" integer NOT NULL, "p2_turns" integer NOT NULL, "logs" text NOT NULL, "run_id" uuid, "p1submission_id" uuid, "p2submission_id" uuid, CONSTRAINT "PK_c82022b5f020870a48610ec2bce" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TYPE "public"."competition_submission_entity_game_type_enum" AS ENUM('CatchTheCat', 'Chess')`);
        await queryRunner.query(`ALTER TABLE "competition_submission_entity" ADD "game_type" "public"."competition_submission_entity_game_type_enum" NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD CONSTRAINT "FK_892a61c7a3d1d8915643c6cfcad" FOREIGN KEY ("run_id") REFERENCES "competition_run_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" ADD CONSTRAINT "FK_9db155cb3344dfc07c41aa97356" FOREIGN KEY ("submission_id") REFERENCES "competition_submission_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_368ba6d197680cb80f377cac145" FOREIGN KEY ("run_id") REFERENCES "competition_run_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_2c8dcf31a9b059b257e995ca9a0" FOREIGN KEY ("p1submission_id") REFERENCES "competition_submission_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_7e2cf74f3f70459c2de10374739" FOREIGN KEY ("p2submission_id") REFERENCES "competition_submission_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_7e2cf74f3f70459c2de10374739"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_2c8dcf31a9b059b257e995ca9a0"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_368ba6d197680cb80f377cac145"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP CONSTRAINT "FK_9db155cb3344dfc07c41aa97356"`);
        await queryRunner.query(`ALTER TABLE "competition_run_submission_report_entity" DROP CONSTRAINT "FK_892a61c7a3d1d8915643c6cfcad"`);
        await queryRunner.query(`ALTER TABLE "competition_submission_entity" DROP COLUMN "game_type"`);
        await queryRunner.query(`DROP TYPE "public"."competition_submission_entity_game_type_enum"`);
        await queryRunner.query(`DROP TABLE "competition_match_entity"`);
        await queryRunner.query(`DROP TYPE "public"."competition_match_entity_winner_enum"`);
        await queryRunner.query(`DROP TABLE "competition_run_entity"`);
        await queryRunner.query(`DROP TABLE "competition_run_submission_report_entity"`);
    }

}
