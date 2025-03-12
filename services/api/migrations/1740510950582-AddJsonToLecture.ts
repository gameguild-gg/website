import { MigrationInterface, QueryRunner } from "typeorm";

export class AddJsonToLecture1740510950582 implements MigrationInterface {
    name = 'AddJsonToLecture1740510950582'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "lecture" ADD "json" jsonb`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "json"`);
    }

}
