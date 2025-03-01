import { MigrationInterface, QueryRunner } from "typeorm";

export class FixPermissions1722886193710 implements MigrationInterface {
    name = 'FixPermissions1722886193710'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_e12875dfb3b1d92d7d7c5377e2"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_189473aaba06ffd667bb024e71"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_7adac5c0b28492eb292d4a9387"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_45bb0502759f0dd73c4fd8b13b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_fda2d885fb612212b85752f5ab"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_f0f25ac3307013265a678db85e"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_55008adb3b4101af12f495c9c1"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_ac2af862c8540eccb210b29310"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_78a916df40e02a9deb1c4b75ed"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_game_roles_gin"`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "bio"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "bio" character varying(256)`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "name"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "name" character varying(256)`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "given_name"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "given_name" character varying(256)`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "family_name"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "family_name" character varying(256)`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "picture"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "picture" character varying(256)`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "post" ADD "thumbnail" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "thumbnail" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "thumbnail" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "thumbnail" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4f1c8311d8e665e0d0c0046326"`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "price"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "price" numeric(7,2) DEFAULT '0'`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_78a916df40e02a9deb1c4b75edb"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "username"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "username" character varying(32)`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_78a916df40e02a9deb1c4b75edb" UNIQUE ("username")`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_e12875dfb3b1d92d7d7c5377e22"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "email" character varying(254)`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_e12875dfb3b1d92d7d7c5377e22" UNIQUE ("email")`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "thumbnail" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "game" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "game" ADD "thumbnail" character varying(1024) DEFAULT ''`);
        await queryRunner.query(`CREATE INDEX "IDX_4f1c8311d8e665e0d0c0046326" ON "course" ("price") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_78a916df40e02a9deb1c4b75ed" ON "user" ("username") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_e12875dfb3b1d92d7d7c5377e2" ON "user" ("email") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_189473aaba06ffd667bb024e71" ON "user" ("facebook_id") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_7adac5c0b28492eb292d4a9387" ON "user" ("google_id") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_45bb0502759f0dd73c4fd8b13b" ON "user" ("github_id") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_fda2d885fb612212b85752f5ab" ON "user" ("apple_id") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_f0f25ac3307013265a678db85e" ON "user" ("linkedin_id") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_55008adb3b4101af12f495c9c1" ON "user" ("twitter_id") `);
        await queryRunner.query(`CREATE UNIQUE INDEX "IDX_ac2af862c8540eccb210b29310" ON "user" ("wallet_address") `);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`DROP INDEX "public"."IDX_ac2af862c8540eccb210b29310"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_55008adb3b4101af12f495c9c1"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_f0f25ac3307013265a678db85e"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_fda2d885fb612212b85752f5ab"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_45bb0502759f0dd73c4fd8b13b"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_7adac5c0b28492eb292d4a9387"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_189473aaba06ffd667bb024e71"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_e12875dfb3b1d92d7d7c5377e2"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_78a916df40e02a9deb1c4b75ed"`);
        await queryRunner.query(`DROP INDEX "public"."IDX_4f1c8311d8e665e0d0c0046326"`);
        await queryRunner.query(`ALTER TABLE "game" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "game" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "proposal" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_e12875dfb3b1d92d7d7c5377e22"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "email" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_e12875dfb3b1d92d7d7c5377e22" UNIQUE ("email")`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_78a916df40e02a9deb1c4b75edb"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "username"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "username" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_78a916df40e02a9deb1c4b75edb" UNIQUE ("username")`);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "price"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "price" double precision DEFAULT '0'`);
        await queryRunner.query(`CREATE INDEX "IDX_4f1c8311d8e665e0d0c0046326" ON "course" ("price") `);
        await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "course" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "chapter" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "lecture" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "thumbnail"`);
        await queryRunner.query(`ALTER TABLE "post" ADD "thumbnail" character varying DEFAULT ''`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "picture"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "picture" character varying`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "family_name"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "family_name" character varying`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "given_name"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "given_name" character varying`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "name"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "name" character varying`);
        await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "bio"`);
        await queryRunner.query(`ALTER TABLE "user_profile" ADD "bio" character varying`);
        await queryRunner.query(`CREATE INDEX "IDX_game_roles_gin" ON "game" ("roles") `);
        await queryRunner.query(`CREATE INDEX "IDX_78a916df40e02a9deb1c4b75ed" ON "user" ("username") `);
        await queryRunner.query(`CREATE INDEX "IDX_ac2af862c8540eccb210b29310" ON "user" ("wallet_address") `);
        await queryRunner.query(`CREATE INDEX "IDX_55008adb3b4101af12f495c9c1" ON "user" ("twitter_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_f0f25ac3307013265a678db85e" ON "user" ("linkedin_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_fda2d885fb612212b85752f5ab" ON "user" ("apple_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_45bb0502759f0dd73c4fd8b13b" ON "user" ("github_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_7adac5c0b28492eb292d4a9387" ON "user" ("google_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_189473aaba06ffd667bb024e71" ON "user" ("facebook_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_e12875dfb3b1d92d7d7c5377e2" ON "user" ("email") `);
    }

}
