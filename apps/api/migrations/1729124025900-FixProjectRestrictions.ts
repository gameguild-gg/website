import { MigrationInterface, QueryRunner } from 'typeorm';

export class FixProjectRestrictions1729124025900 implements MigrationInterface {
  name = 'FixProjectRestrictions1729124025900';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "tag" DROP COLUMN "deleted_at"`);
    await queryRunner.query(
      `ALTER TABLE "user_profile" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_submission_report_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_match_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_submission_entity" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "user" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "deleted_at"`);
    await queryRunner.query(`ALTER TABLE "vote" DROP COLUMN "deleted_at"`);
    await queryRunner.query(
      `ALTER TABLE "project_feedback_response" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(
      `ALTER TABLE "project_version" DROP COLUMN "deleted_at"`,
    );
    await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "deleted_at"`);
    await queryRunner.query(
      `ALTER TABLE "post" DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_e28aa0c4114146bfb1567bfa9a"`,
    );
    await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "post" ADD "title" character varying(255) NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_4c8c4ea44b8a5a5c6a028b9e20"`,
    );
    await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "lecture" ADD "title" character varying(255) NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" DROP CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_1540f666b7a73b75b2c13e5db2"`,
    );
    await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "chapter" ADD "title" character varying(255) NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_ac5edecc1aefa58ed0237a7ee4"`,
    );
    await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "course" ADD "title" character varying(255) NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" DROP CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`,
    );
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD "title" character varying(255) NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_244c4f211882ced89a917ec4f3"`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`,
    );
    await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "project" ADD "title" character varying(255) NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "summary" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "body" DROP NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "owner_id" DROP NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_e28aa0c4114146bfb1567bfa9a" ON "post" ("title") `,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_4c8c4ea44b8a5a5c6a028b9e20" ON "lecture" ("title") `,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_1540f666b7a73b75b2c13e5db2" ON "chapter" ("title") `,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_ac5edecc1aefa58ed0237a7ee4" ON "course" ("title") `,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `,
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
      `DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_a84bb70f034d1ba5605cd3fbc5"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_244c4f211882ced89a917ec4f3"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_175c19011e5f5baf732945a311"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_ac5edecc1aefa58ed0237a7ee4"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_1540f666b7a73b75b2c13e5db2"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_4c8c4ea44b8a5a5c6a028b9e20"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_e28aa0c4114146bfb1567bfa9a"`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(`ALTER TABLE "project" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "project" ADD "title" character varying(1024) NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON "project" ("title") `,
    );
    await queryRunner.query(
      `ALTER TABLE "project" ADD CONSTRAINT "FK_d40afe32d1d771bea7a5f468185" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(`ALTER TABLE "proposal" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD "title" character varying(1024) NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON "proposal" ("title") `,
    );
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD CONSTRAINT "FK_5cb547a3ae3fcca0bcd8843bc29" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(`ALTER TABLE "course" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "course" ADD "title" character varying(1024) NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_ac5edecc1aefa58ed0237a7ee4" ON "course" ("title") `,
    );
    await queryRunner.query(
      `ALTER TABLE "course" ADD CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(`ALTER TABLE "chapter" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "chapter" ADD "title" character varying(1024) NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_1540f666b7a73b75b2c13e5db2" ON "chapter" ("title") `,
    );
    await queryRunner.query(
      `ALTER TABLE "chapter" ADD CONSTRAINT "FK_b065fd0c7a5c1f03aa2e313741f" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(`ALTER TABLE "lecture" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "lecture" ADD "title" character varying(1024) NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_4c8c4ea44b8a5a5c6a028b9e20" ON "lecture" ("title") `,
    );
    await queryRunner.query(
      `ALTER TABLE "lecture" ADD CONSTRAINT "FK_e61feac3d2667f7f077955c2891" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "owner_id" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "body" SET NOT NULL`,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ALTER COLUMN "summary" SET NOT NULL`,
    );
    await queryRunner.query(`ALTER TABLE "post" DROP COLUMN "title"`);
    await queryRunner.query(
      `ALTER TABLE "post" ADD "title" character varying(1024) NOT NULL`,
    );
    await queryRunner.query(
      `CREATE INDEX "IDX_e28aa0c4114146bfb1567bfa9a" ON "post" ("title") `,
    );
    await queryRunner.query(
      `ALTER TABLE "post" ADD CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c" FOREIGN KEY ("owner_id") REFERENCES "user"("id") ON DELETE NO ACTION ON UPDATE NO ACTION`,
    );
    await queryRunner.query(`ALTER TABLE "project" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `ALTER TABLE "project_version" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "project_feedback_response" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(`ALTER TABLE "vote" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `ALTER TABLE "proposal" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(`ALTER TABLE "user" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "course" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "chapter" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "lecture" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(`ALTER TABLE "post" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `ALTER TABLE "competition_submission_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_match_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "competition_run_submission_report_entity" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(
      `ALTER TABLE "user_profile" ADD "deleted_at" TIMESTAMP`,
    );
    await queryRunner.query(`ALTER TABLE "tag" ADD "deleted_at" TIMESTAMP`);
    await queryRunner.query(
      `DROP INDEX "public"."IDX_fa0b822cf2b8a43e0cacb7af32"`,
    );
    await queryRunner.query(
      `DROP INDEX "public"."IDX_4e23c779aed4219bd3db40da22"`,
    );
  }
}
