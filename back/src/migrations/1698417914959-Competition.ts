import { MigrationInterface, QueryRunner } from "typeorm";

export class Competition1698417914959 implements MigrationInterface {
    name = 'Competition1698417914959'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_1b2e0d15a243294ae6760939e14"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_b91a86654b54cb05a2fc8f95f0d"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_1b2e0d15a243294ae6760939e14" FOREIGN KEY ("catcher_id") REFERENCES "competition_submission_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_b91a86654b54cb05a2fc8f95f0d" FOREIGN KEY ("cat_id") REFERENCES "competition_submission_entity"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_b91a86654b54cb05a2fc8f95f0d"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" DROP CONSTRAINT "FK_1b2e0d15a243294ae6760939e14"`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_b91a86654b54cb05a2fc8f95f0d" FOREIGN KEY ("cat_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "competition_match_entity" ADD CONSTRAINT "FK_1b2e0d15a243294ae6760939e14" FOREIGN KEY ("catcher_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
