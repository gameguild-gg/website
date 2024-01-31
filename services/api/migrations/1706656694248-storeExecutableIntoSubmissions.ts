import { MigrationInterface, QueryRunner } from "typeorm";

export class StoreExecutableIntoSubmissions1706656694248 implements MigrationInterface {
    name = 'StoreExecutableIntoSubmissions1706656694248'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_submission_entity" ADD "executable" bytea`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_submission_entity" DROP COLUMN "executable"`);
    }

}
