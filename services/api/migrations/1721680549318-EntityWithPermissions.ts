import { MigrationInterface, QueryRunner } from "typeorm";

export class EntityWithPermissions1721680549318 implements MigrationInterface {
    name = 'EntityWithPermissions1721680549318'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user_roles" ADD "resource_type" character varying(255) NOT NULL`);
        await queryRunner.query(`CREATE INDEX "IDX_4d94147dd97fb22ec638c7fa34" ON "user_roles" ("resource_type") `);
        await queryRunner.query(`ALTER TABLE "user_roles" ADD CONSTRAINT "user_role_resource" UNIQUE ("user_id", "role", "resource_id", "resource_type")`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user_roles" DROP CONSTRAINT "user_role_resource"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4d94147dd97fb22ec638c7fa34"`);
        await queryRunner.query(`ALTER TABLE "user_roles" DROP COLUMN "resource_type"`);
    }

}
