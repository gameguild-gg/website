import { MigrationInterface, QueryRunner } from 'typeorm';

export class EntityWithPermissions1721706066467 implements MigrationInterface {
  name = 'EntityWithPermissions1721706066467';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(
      `ALTER TABLE "game" DROP CONSTRAINT "FK_678fcc30dbaf1c4c7e86bc10d16"`,
    );
    await queryRunner.query(
      `ALTER TABLE "game" RENAME COLUMN "owner_id" TO "roles"`,
    );
    await queryRunner.query(`ALTER TABLE "game" DROP COLUMN "roles"`);
    await queryRunner.query(`ALTER TABLE "game" ADD "roles" jsonb`);
    await queryRunner.query(
      `CREATE INDEX "IDX_game_roles_gin" ON "game" USING gin (roles jsonb_path_ops)`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`DROP INDEX "IDX_game_roles_gin"`);
    await queryRunner.query(`ALTER TABLE "game" DROP COLUMN "roles"`);
    await queryRunner.query(`ALTER TABLE "game" ADD "roles" uuid`);
    await queryRunner.query(
      `ALTER TABLE "game" RENAME COLUMN "roles" TO "owner_id"`,
    );
    await queryRunner.query(
      `ALTER TABLE "game" ADD CONSTRAINT "FK_678fcc30dbaf1c4c7e86bc10d16" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
  }
}
