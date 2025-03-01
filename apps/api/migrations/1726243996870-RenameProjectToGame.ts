import { MigrationInterface, QueryRunner } from "typeorm";

export class RenameProjectToGame1726243996870 implements MigrationInterface {
    name = 'RenameProjectToGame1726243996870'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project_version" DROP CONSTRAINT "FK_f5931748d5514ab10ade223d431"`);
        await queryRunner.query(`ALTER TABLE "project_version" DROP CONSTRAINT "UQ_4aa15ae20ae619ff1562db73861"`);
        await queryRunner.query(`ALTER TABLE "project_version" RENAME COLUMN "game_id" TO "project_id"`);
        await queryRunner.query(`ALTER TABLE "project_version" ADD CONSTRAINT "UQ_b9103221778444dc9638d3775da" UNIQUE ("version", "project_id")`);
        await queryRunner.query(`ALTER TABLE "project_version" ADD CONSTRAINT "FK_2ffeb709a46febd677fee6a29d4" FOREIGN KEY ("project_id") REFERENCES "project"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "project_version" DROP CONSTRAINT "FK_2ffeb709a46febd677fee6a29d4"`);
        await queryRunner.query(`ALTER TABLE "project_version" DROP CONSTRAINT "UQ_b9103221778444dc9638d3775da"`);
        await queryRunner.query(`ALTER TABLE "project_version" RENAME COLUMN "project_id" TO "game_id"`);
        await queryRunner.query(`ALTER TABLE "project_version" ADD CONSTRAINT "UQ_4aa15ae20ae619ff1562db73861" UNIQUE ("version", "game_id")`);
        await queryRunner.query(`ALTER TABLE "project_version" ADD CONSTRAINT "FK_f5931748d5514ab10ade223d431" FOREIGN KEY ("game_id") REFERENCES "project"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
    }

}
