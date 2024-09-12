import { MigrationInterface, QueryRunner } from "typeorm";

export class AddGameFeedbackResponse1726083205884 implements MigrationInterface {
    name = 'AddGameFeedbackResponse1726083205884'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "game_feedback_response" ADD "responses" jsonb NOT NULL`);
        await queryRunner.query(`ALTER TABLE "game_version" DROP COLUMN "feedback_form"`);
        await queryRunner.query(`ALTER TABLE "game_version" ADD "feedback_form" jsonb NOT NULL`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "game_version" DROP COLUMN "feedback_form"`);
        await queryRunner.query(`ALTER TABLE "game_version" ADD "feedback_form" character varying(1024) NOT NULL`);
        await queryRunner.query(`ALTER TABLE "game_feedback_response" DROP COLUMN "responses"`);
    }

}
