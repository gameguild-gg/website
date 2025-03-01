import { MigrationInterface, QueryRunner } from 'typeorm';

export class AddProfileFieldsToProfileEntity1718406251311
  implements MigrationInterface
{
  name = 'AddProfileFieldsToProfileEntity1718406251311';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "bio" character varying`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "name" character varying`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "given_name" character varying`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "family_name" character varying`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "picture" character varying`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "picture"`);
    await queryRunner.query(
      `ALTER TABLE "user_profile" DROP COLUMN "family_name"`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" DROP COLUMN "given_name"`,
    );
    await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "name"`);
    await queryRunner.query(`ALTER TABLE "user_profile" DROP COLUMN "bio"`);
  }
}
