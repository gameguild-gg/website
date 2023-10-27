import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1698413269088 implements MigrationInterface {
    name = 'Competition1698413269088'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TYPE "public"."competition_match_entity_winner_enum" AS ENUM('CAT', 'CATCHER')`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "winner" "public"."competition_match_entity_winner_enum"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "cat_turns" integer NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "catcher_turns" integer NOT NULL`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "catcher_turns"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "cat_turns"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "winner"`);
        await queryRunner.query(`DROP TYPE "public"."competition_match_entity_winner_enum"`);
    }

}
