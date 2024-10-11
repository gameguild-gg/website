import { MigrationInterface, QueryRunner } from 'typeorm';

export class AddDeletedAtFieldToEntityBase1728672029215
  implements MigrationInterface
{
  name = 'AddDeletedAtFieldToEntityBase1728672029215';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "description"`);
    await queryRunner.query(`ALTER TABLE "tag" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_submission_report_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_match_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_submission_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(`ALTER TABLE "post" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "lecture" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "chapter" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "course" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "user" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(`ALTER TABLE "vote" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "project" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `ALTER TABLE "project_feedback_response" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "project_version" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" DROP CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29"`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "title" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ADD CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ADD CONSTRAINT "FK_e61feac3d2667f7f077955c2891" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ADD CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ADD CONSTRAINT "FK_d40afe32d1d771bea7a5f468185" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(
      `ALTER TABLE "project" DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" DROP CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29"`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "title" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ADD CONSTRAINT "FK_d40afe32d1d771bea7a5f468185" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ADD CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ADD CONSTRAINT "FK_e61feac3d2667f7f077955c2891" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ADD CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "project_version" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "project_feedback_response" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "vote" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "deleted_at"`);
    await queryRunner.query(
      `ALTER TABLE "competition_submission_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_match_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(`ALTER TABLE "tag" DROP COLUMN "deleted_at"`);
    await queryRunner.query(
      `ALTER TABLE "project" ADD "description" character varying(255)`,
    );
  }
}
