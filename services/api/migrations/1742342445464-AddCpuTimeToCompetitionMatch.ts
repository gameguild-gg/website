import { MigrationInterface, QueryRunner } from "typeorm";

export class AddCpuTimeToCompetitionMatch1742342445464 implements MigrationInterface {
    name = 'AddCpuTimeToCompetitionMatch1742342445464'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "p1cpu_time" real NOT NULL DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD "p2cpu_time" real NOT NULL DEFAULT '0'`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "p2cpu_time"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP COLUMN "p1cpu_time"`);
    }

}
