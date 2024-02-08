import { MigrationInterface, QueryRunner } from "typeorm";

export class ChangeDefaultEloValue1707429988620 implements MigrationInterface {
    name = 'ChangeDefaultEloValue1707429988620'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "elo" SET DEFAULT '400'`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "elo" SET DEFAULT '1000'`);
    }

}
