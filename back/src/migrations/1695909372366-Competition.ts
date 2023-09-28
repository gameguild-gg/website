import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1695909372366 implements MigrationInterface {
    name = 'Competition1695909372366'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "competition_submission_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "source_code_zip" bytea NOT NULL, "user_id" uuid, CONSTRAINT "PK_6af6334fc1cff3b8b08d2b40cb0" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "password"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "password_hash" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "password_salt" character varying`);
        await queryRunner.query(`ALTER TABLE "competition_submission_entity" ADD CONSTRAINT "FK_75ec33915040f6bcb612f9a74c2" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_submission_entity" DROP CONSTRAINT "FK_75ec33915040f6bcb612f9a74c2"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "password_salt"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "password_hash"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "password" character varying`);
        await queryRunner.query(`DROP TABLE "competition_submission_entity"`);
    }

}
