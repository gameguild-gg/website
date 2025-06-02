import { MigrationInterface, QueryRunner } from 'typeorm';

export class DropOldCmsEntities1748883584649 implements MigrationInterface {
  name = 'DropOldCmsEntities1748883584649';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP CONSTRAINT "FK_eb16ef58c3c71cf333b9cc106f0"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP CONSTRAINT "FK_17cc52872004472c7a8125fd789"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP CONSTRAINT "FK_736041642ce62379cad4a41b6c8"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP CONSTRAINT "FK_be83e259b70f9b1d2eb25bd049c"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP CONSTRAINT "FK_2a091991ca5b2917a9be369912b"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP CONSTRAINT "FK_a9fe4081c01805449159cbf96a1"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP CONSTRAINT "FK_d40afe32d1d771bea7a5f468185"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP CONSTRAINT "FK_2ffeb709a46febd677fee6a29d4"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP CONSTRAINT "FK_d01ed37f3d5ac13bca59ba2956e"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP CONSTRAINT "FK_b321f272db3baa8decf94446c16"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP CONSTRAINT "FK_f1326fde621b704a33759e54fe5"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP CONSTRAINT "FK_b15e7d39aa644053a0321fc9e7c"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP CONSTRAINT "FK_08bec6f2e24762d264e0a1e29d8"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP CONSTRAINT "FK_e61feac3d2667f7f077955c2891"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP CONSTRAINT "FK_8617e94525034849ee1b9b2e2f1"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP CONSTRAINT "FK_39231fcb10d7a7ff129ce7e51fc"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP CONSTRAINT "FK_e0c2ee13a4c2bc7efc299d32b65"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_80e0595aa5572aca26e681ee1e"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_91b3636bd5cc303c7409c55088"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_37105dd2b728dfbba2f313c907"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_6fce32ddd71197807027be6ad3"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_0c2943d87d6b4f22558a7fce71"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_cb001317127de4d5e323b5c0c4"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_4b6747e098d96a0e805692810d"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_a80ca3bf4ca3711c488cb82cf7"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_cd1bddce36edc3e766798eab37"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_e28aa0c4114146bfb1567bfa9a"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_f5cadec277927351eb00098dd7"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_c6c3264ce6966d5c1d078e147b"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_dff3c45c6a2688bdef948f158a"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_4c8c4ea44b8a5a5c6a028b9e20"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_d316a6a4d94b0b4aaab3af3f31"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_653293097239013a0e49f481f9"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_399d4e499683d688c07a41da26"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_a101f48e5045bcf501540a4a5b"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_4f1c8311d8e665e0d0c0046326"`);
    await queryRunner.query(`DROP INDEX "public"."IDX_ac5edecc1aefa58ed0237a7ee4"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP CONSTRAINT "UQ_b9103221778444dc9638d3775da"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "status"`);
    await queryRunner.query(`DROP TYPE "public"."ticket_status_enum"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "priority"`);
    await queryRunner.query(`DROP TYPE "public"."ticket_priority_enum"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "owner_id"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "project_id"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "title"`);
    await queryRunner.query(`ALTER TABLE "ticket"
      DROP COLUMN "description"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "visibility"`);
    await queryRunner.query(`DROP TYPE "public"."quiz_visibility_enum"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "questions"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "owner_id"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "thumbnail_id"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "slug"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "title"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "summary"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "body"`);
    await queryRunner.query(`ALTER TABLE "quiz"
      DROP COLUMN "grading_instructions"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "visibility"`);
    await queryRunner.query(`DROP TYPE "public"."project_visibility_enum"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "owner_id"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "thumbnail_id"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "banner_id"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "slug"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "summary"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "body"`);
    await queryRunner.query(`ALTER TABLE "project"
      DROP COLUMN "title"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "feedback_form"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "feedback_deadline"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "project_id"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "version"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "archive_url"`);
    await queryRunner.query(`ALTER TABLE "project_version"
      DROP COLUMN "notes_url"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP COLUMN "responses"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP COLUMN "version_id"`);
    await queryRunner.query(`ALTER TABLE "project_feedback_response"
      DROP COLUMN "user_id"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "visibility"`);
    await queryRunner.query(`DROP TYPE "public"."post_visibility_enum"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "post_type"`);
    await queryRunner.query(`DROP TYPE "public"."post_post_type_enum"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "owner_id"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "thumbnail_id"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "body"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "title"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "slug"`);
    await queryRunner.query(`ALTER TABLE "post"
      DROP COLUMN "summary"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "visibility"`);
    await queryRunner.query(`DROP TYPE "public"."lecture_visibility_enum"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "order"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "course_id"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "owner_id"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "thumbnail_id"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "renderer"`);
    await queryRunner.query(`DROP TYPE "public"."lecture_renderer_enum"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "json"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "slug"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "summary"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "body"`);
    await queryRunner.query(`ALTER TABLE "lecture"
      DROP COLUMN "title"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "created_at"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "updated_at"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "visibility"`);
    await queryRunner.query(`DROP TYPE "public"."course_visibility_enum"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "subscription_access"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "price"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "owner_id"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "thumbnail_id"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "slug"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "summary"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "body"`);
    await queryRunner.query(`ALTER TABLE "course"
      DROP COLUMN "title"`);

    // drop tables - first drop join/linking tables that have foreign keys
    await queryRunner.query(`DROP TABLE IF EXISTS "post_editors_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "project_editors_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "lecture_editors_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "course_editors_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "ticket_editors_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "project_screenshots_images"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "quiz_editors_user"`);

    // drop dependent tables that reference other tables
    await queryRunner.query(`DROP TABLE IF EXISTS "project_feedback_response"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "project_version"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "ticket"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "lecture"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "chapter"`);

    // drop main tables
    await queryRunner.query(`DROP TABLE IF EXISTS "quiz"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "project"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "post"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "course"`);
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    // irreversible migration
  }
}
