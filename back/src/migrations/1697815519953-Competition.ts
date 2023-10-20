import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1697815519953 implements MigrationInterface {
    name = 'Competition1697815519953'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "competition_run_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "is_running" boolean NOT NULL DEFAULT false, CONSTRAINT "PK_c2c36337de9af6a41752324bc01" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE TABLE "competition_match_entity" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "cat_points" double precision NOT NULL, "catcher_points" double precision NOT NULL, "competition_run_id" uuid, "catcher_id" uuid, "cat_id" uuid, CONSTRAINT "PK_c82022b5f020870a48610ec2bce" PRIMARY KEY ("id"))`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_4a38724ebc328cadda60121090d" FOREIGN KEY ("competition_run_id") REFERENCES "competition_run_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_1b2e0d15a243294ae6760939e14" FOREIGN KEY ("catcher_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_b91a86654b54cb05a2fc8f95f0d" FOREIGN KEY ("cat_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_b91a86654b54cb05a2fc8f95f0d"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_1b2e0d15a243294ae6760939e14"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_4a38724ebc328cadda60121090d"`);
        await queryRunner.query(`DROP TABLE "competition_match_entity"`);
        await queryRunner.query(`DROP TABLE "competition_run_entity"`);
    }

}
