import { MigrationInterface, QueryRunner } from 'typeorm';

export class CleanUnUsedTables1740414017299 implements MigrationInterface {
  name = 'CleanUnUsedTables1740414017299';

  public async up(queryRunner: QueryRunner): Promise<void> {
    // Drop foreign key constraints first
    await queryRunner.query(`ALTER TABLE "event"
        DROP CONSTRAINT IF EXISTS "event_creator_id_fkey"`);
    await queryRunner.query(`ALTER TABLE "game"
        DROP CONSTRAINT IF EXISTS "game_owner_id_fkey"`);
    await queryRunner.query(`ALTER TABLE "game_feedback_response"
        DROP CONSTRAINT IF EXISTS "game_feedback_response_version_id_fkey"`);
    await queryRunner.query(`ALTER TABLE "game_feedback_response"
        DROP CONSTRAINT IF EXISTS "game_feedback_response_user_id_fkey"`);
    await queryRunner.query(`ALTER TABLE "game_version"
        DROP CONSTRAINT IF EXISTS "game_version_game_id_fkey"`);

    // Drop tables in correct order
    await queryRunner.query(`DROP TABLE IF EXISTS "game_feedback_response"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "game_version"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "game_team_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "game_editors_user"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "user_roles"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "user_posts_post"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "game"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "event"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "ipfs"`);
    await queryRunner.query(`DROP TABLE IF EXISTS "user-profile"`);
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    // Restore tables in reverse order
    await queryRunner.query(`CREATE TABLE "user-profile"
                             (
                                 "id"         uuid                        NOT NULL DEFAULT uuid_generate_v4(),
                                 "created_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "full_name"  character varying           NOT NULL,
                                 "cpf"        character varying           NOT NULL
                             );`);

    await queryRunner.query(`CREATE TABLE "ipfs"
                             (
                                 "id"         uuid                        NOT NULL DEFAULT uuid_generate_v4(),
                                 "created_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "cid"        character varying           NOT NULL,
                                 "mime_type"  character varying           NOT NULL,
                                 "file_name"  character varying           NOT NULL
                             );`);

    await queryRunner.query(`CREATE TABLE "event"
                             (
                                 "id"         uuid                        NOT NULL DEFAULT uuid_generate_v4(),
                                 "created_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "title"      character varying           NOT NULL,
                                 "day"        timestamp without time zone NOT NULL,
                                 "time"       timestamp without time zone NOT NULL,
                                 "type"       character varying           NOT NULL,
                                 "creator_id" uuid,
                                 CONSTRAINT "event_creator_id_fkey" FOREIGN KEY ("creator_id") REFERENCES "user-profile" ("id") ON DELETE CASCADE
                             );`);

    await queryRunner.query(`CREATE TABLE "game"
                             (
                                 "id"         uuid                        NOT NULL DEFAULT uuid_generate_v4(),
                                 "created_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
                                 "slug"       character varying(255)      NOT NULL,
                                 "title"      character varying(1024)     NOT NULL,
                                 "summary"    character varying(1024)     NOT NULL,
                                 "visibility" character varying           NOT NULL DEFAULT 'DRAFT',
                                 "body"       text                        NOT NULL,
                                 "thumbnail"  character varying(1024)              DEFAULT '',
                                 "owner_id"   uuid                        NOT NULL,
                                 CONSTRAINT "game_owner_id_fkey" FOREIGN KEY ("owner_id") REFERENCES "user-profile" ("id") ON DELETE CASCADE
                             );`);

    await queryRunner.query(`CREATE TABLE "user_posts_post"
                             (
                                 "user_id" uuid NOT NULL,
                                 "post_id" uuid NOT NULL
                             );`);

    await queryRunner.query(`CREATE TABLE "game_team_user"
                             (
                                 "game_id" uuid NOT NULL,
                                 "user_id" uuid NOT NULL
                             );`);

    await queryRunner.query(`CREATE TABLE "game_editors_user"
                             (
                                 "game_id" uuid NOT NULL,
                                 "user_id" uuid NOT NULL
                             );`);

    await queryRunner.query(`CREATE TABLE "user_roles"
                             (
                                 "id"            uuid                        NOT NULL DEFAULT uuid_generate_v4(),
                                 "created_at"    timestamp without time zone NOT NULL DEFAULT now(),
                                 "updated_at"    timestamp without time zone NOT NULL DEFAULT now(),
                                 "role"          character varying           NOT NULL DEFAULT 'CONTENT_OWNER',
                                 "resource_id"   uuid                        NOT NULL,
                                 "user_id"       uuid,
                                 "resource_type" character varying(255)      NOT NULL
                             );`);
  }
}
