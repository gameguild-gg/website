import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1698322840342 implements MigrationInterface {
    name = 'Competition1698322840342'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "logs" text NOT NULL`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "logs"`);
    }

}
