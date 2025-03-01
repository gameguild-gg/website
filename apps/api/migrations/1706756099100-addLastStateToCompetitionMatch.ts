import { MigrationInterface, QueryRunner } from "typeorm";

export class AddLastStateToCompetitionMatch1706756099100 implements MigrationInterface {
    name = 'AddLastStateToCompetitionMatch1706756099100'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "last_state" text`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ALTER COLUMN "logs" DROP NOT NULL`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ALTER COLUMN "logs" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "last_state"`);
    }

}
