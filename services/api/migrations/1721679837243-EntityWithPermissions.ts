import { MigrationInterface, QueryRunner } from "typeorm";

export class EntityWithPermissions1721679837243 implements MigrationInterface {
    name = 'EntityWithPermissions1721679837243'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`CREATE TABLE "user_roles" ("id" uuid NOT NULL DEFAULT uuid_generate_v4(), "created_at" TIMESTAMP NOT NULL DEFAULT now(), "updated_at" TIMESTAMP NOT NULL DEFAULT now(), "role" character varying NOT NULL DEFAULT 'CONTENT_OWNER', "resource_id" uuid NOT NULL, "user_id" uuid, CONSTRAINT "PK_8acd5cf26ebd158416f477de799" PRIMARY KEY ("id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_0475850442d60bd704c5804155" ON "user_roles" ("role") `);
        await queryRunner.query(`CREATE INDEX "IDX_dd3f70fdb9082542d648df377b" ON "user_roles" ("resource_id") `);
        await queryRunner.query(`CREATE TABLE "game_editors_user" ("game_id" uuid NOT NULL, "user_id" uuid NOT NULL, CONSTRAINT "PK_214626b3d397af569e87a86a9d2" PRIMARY KEY ("game_id", "user_id"))`);
        await queryRunner.query(`CREATE INDEX "IDX_6e34a3d0ce4c6be0f42c4ad3ef" ON "game_editors_user" ("game_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_c502bad85d59f1c288baa97c4f" ON "game_editors_user" ("user_id") `);
        await queryRunner.query(`ALTER TABLE "user_roles" ADD CONSTRAINT "FK_87b8888186ca9769c960e926870" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`);
        await queryRunner.query(`ALTER TABLE "game_editors_user" ADD CONSTRAINT "FK_6e34a3d0ce4c6be0f42c4ad3ef0" FOREIGN KEY ("game_id") REFERENCES "game"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
        await queryRunner.query(`ALTER TABLE "game_editors_user" ADD CONSTRAINT "FK_c502bad85d59f1c288baa97c4ff" FOREIGN KEY ("user_id") REFERENCES "user"("id") ON DELETE CASCADE ON UPDATE CASCADE`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "game_editors_user" DROP CONSTRAINT "FK_c502bad85d59f1c288baa97c4ff"`);
        await queryRunner.query(`ALTER TABLE "game_editors_user" DROP CONSTRAINT "FK_6e34a3d0ce4c6be0f42c4ad3ef0"`);
        await queryRunner.query(`ALTER TABLE "user_roles" DROP CONSTRAINT "FK_87b8888186ca9769c960e926870"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_c502bad85d59f1c288baa97c4f"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_6e34a3d0ce4c6be0f42c4ad3ef"`);
        await queryRunner.query(`DROP TABLE "game_editors_user"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_dd3f70fdb9082542d648df377b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_0475850442d60bd704c5804155"`);
        await queryRunner.query(`DROP TABLE "user_roles"`);
    }

}
