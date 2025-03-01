import { MigrationInterface, QueryRunner } from "typeorm";

export class UpdateUserUniqueness1718396081310 implements MigrationInterface {
    name = 'UpdateUserUniqueness1718396081310'

    public async up(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "email" DROP NOT NULL`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_189473aaba06ffd667bb024e71a" UNIQUE ("facebook_id")`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_7adac5c0b28492eb292d4a93871" UNIQUE ("google_id")`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_45bb0502759f0dd73c4fd8b13bd" UNIQUE ("github_id")`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_fda2d885fb612212b85752f5ab1" UNIQUE ("apple_id")`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_f0f25ac3307013265a678db85e4" UNIQUE ("linkedin_id")`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_55008adb3b4101af12f495c9c1d" UNIQUE ("twitter_id")`);
        await queryRunner.query(`ALTER TABLE "user" ADD CONSTRAINT "UQ_ac2af862c8540eccb210b293107" UNIQUE ("wallet_address")`);
    }

    public async down(queryRunner: QueryRunner): Promise<void> {
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_ac2af862c8540eccb210b293107"`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_55008adb3b4101af12f495c9c1d"`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_f0f25ac3307013265a678db85e4"`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_fda2d885fb612212b85752f5ab1"`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_45bb0502759f0dd73c4fd8b13bd"`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_7adac5c0b28492eb292d4a93871"`);
        await queryRunner.query(`ALTER TABLE "user" DROP CONSTRAINT "UQ_189473aaba06ffd667bb024e71a"`);
        await queryRunner.query(`ALTER TABLE "user" ALTER COLUMN "email" SET NOT NULL`);
    }

}
