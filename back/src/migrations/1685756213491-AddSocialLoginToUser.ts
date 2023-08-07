import { MigrationInterface, QueryRunner } from "typeorm";

export class AddSocialLoginToUser1685756213491 implements MigrationInterface {
    name = 'AddSocialLoginToUser1685756213491'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "active"`);
        await queryRunner.query(`ALTER TABLE "user-profile" DROP COLUMN "email"`);
        await queryRunner.query(`ALTER TABLE "user" ADD "email" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD "email_validated" boolean NOT NULL DEFAULT false`);
        await queryRunner.query(`ALTER TABLE "user" ADD "password" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "facebook_id" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "google_id" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "github_id" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "apple_id" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "linkedin_id" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "twitter_id" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ADD "wallet_address" character varying`);
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "username" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "role" SET DEFAULT 'User'`);
        await queryRunner.query(`CREATE INDEX "IDX_e12875dfb3b1d92d7d7c5377e2" ON "user" ("email") `);
        await queryRunner.query(`CREATE INDEX "IDX_189473aaba06ffd667bb024e71" ON "user" ("facebook_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_7adac5c0b28492eb292d4a9387" ON "user" ("google_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_45bb0502759f0dd73c4fd8b13b" ON "user" ("github_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_fda2d885fb612212b85752f5ab" ON "user" ("apple_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_f0f25ac3307013265a678db85e" ON "user" ("linkedin_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_55008adb3b4101af12f495c9c1" ON "user" ("twitter_id") `);
        await queryRunner.query(`CREATE INDEX "IDX_ac2af862c8540eccb210b29310" ON "user" ("wallet_address") `);
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
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "role" DROP DEFAULT`);
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "username" SET NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "wallet_address"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "twitter_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "linkedin_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "apple_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "github_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "google_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "facebook_id"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "password"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email_validated"`);
        await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "email"`);
        await queryRunner.query(`ALTER TABLE "user-profile" ADD "email" character varying NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD "active" boolean NOT NULL`);
    }

}
