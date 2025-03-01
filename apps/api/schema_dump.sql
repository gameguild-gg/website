CREATE TYPE "chapter_visibility_enum" AS ENUM ('DRAFT', 'TRASH', 'PRIVATE', 'PENDING', 'FUTURE', 'PUBLISHED');

CREATE TYPE "competition_match_entity_winner_enum" AS ENUM ('Player1', 'Player2');

CREATE TYPE "competition_submission_entity_game_type_enum" AS ENUM ('CatchTheCat', 'Chess');

CREATE TYPE "course_visibility_enum" AS ENUM ('PRIVATE', 'PUBLISHED', 'FUTURE', 'PENDING', 'TRASH', 'DRAFT');

CREATE TYPE "game_visibility_enum" AS ENUM ('PUBLISHED', 'TRASH', 'PRIVATE', 'PENDING', 'DRAFT', 'FUTURE');

CREATE TYPE "job_post_job_type_enum" AS ENUM ('TASK', 'CONTINUOUS');

CREATE TYPE "job_post_visibility_enum" AS ENUM ('DRAFT', 'TRASH', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE');

CREATE TYPE "lecture_renderer_enum" AS ENUM ('markdown', 'youtube', 'lexical', 'reveal', 'pdf', 'html', 'code', 'audio', 'video', 'image', 'link');

CREATE TYPE "lecture_visibility_enum" AS ENUM ('PUBLISHED', 'PENDING', 'PRIVATE', 'TRASH', 'DRAFT', 'FUTURE');

CREATE TYPE "post_post_type_enum" AS ENUM ('BLOG');

CREATE TYPE "post_visibility_enum" AS ENUM ('FUTURE', 'DRAFT', 'PUBLISHED', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "project_visibility_enum" AS ENUM ('PUBLISHED', 'DRAFT', 'TRASH', 'PRIVATE', 'PENDING', 'FUTURE');

CREATE TYPE "proposal_visibility_enum" AS ENUM ('TRASH', 'DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE');

CREATE TYPE "quiz_visibility_enum" AS ENUM ('PUBLISHED', 'DRAFT', 'TRASH', 'PRIVATE', 'PENDING', 'FUTURE');

CREATE TYPE "ticket_priority_enum" AS ENUM ('LOW', 'CRITICAL', 'HIGH', 'MEDIUM');

CREATE TYPE "ticket_status_enum" AS ENUM ('IN_PROGRESS', 'CLOSED', 'RESOLVED', 'OPEN');

CREATE TABLE "migrations" (
  "id" integer NOT NULL DEFAULT nextval('migrations_id_seq'::regclass),
  "timestamp" bigint NOT NULL,
  "name" character varying NOT NULL
);

ALTER TABLE "migrations"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_8c82d7f526340ab734260ea46be" ON public.migrations USING btree (id);

CREATE TABLE "quiz" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "title" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "body" text,
  "visibility" "quiz_visibility_enum" NOT NULL DEFAULT 'DRAFT'::quiz_visibility_enum,
  "questions" jsonb,
  "grading_instructions" character varying,
  "owner_id" uuid,
  "thumbnail_id" uuid
);

ALTER TABLE "quiz"
            ADD PRIMARY KEY (id);  ALTER TABLE "quiz"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "quiz"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  CREATE UNIQUE INDEX "PK_422d974e7217414e029b3e641d0" ON public.quiz USING btree (id);
CREATE UNIQUE INDEX "IDX_80e0595aa5572aca26e681ee1e" ON public.quiz USING btree (slug);
CREATE INDEX "IDX_91b3636bd5cc303c7409c55088" ON public.quiz USING btree (title);
CREATE INDEX "IDX_37105dd2b728dfbba2f313c907" ON public.quiz USING btree (visibility);

CREATE TABLE "quiz_editors_user" (
  "quiz_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "quiz_editors_user"
            ADD PRIMARY KEY (quiz_id, user_id);  ALTER TABLE "quiz_editors_user"
            ADD FOREIGN KEY (quiz_id) REFERENCES quiz(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "quiz_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_fbb93aa1e4225ccb62d11a796c0" ON public.quiz_editors_user USING btree (quiz_id, user_id);
CREATE INDEX "IDX_536f6eac3215f5823c76623df1" ON public.quiz_editors_user USING btree (quiz_id);
CREATE INDEX "IDX_f09c6797b5611ed29a94d7df8b" ON public.quiz_editors_user USING btree (user_id);

CREATE TABLE "post_editors_user" (
  "post_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "post_editors_user"
            ADD PRIMARY KEY (post_id, user_id);  ALTER TABLE "post_editors_user"
            ADD FOREIGN KEY (post_id) REFERENCES post(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "post_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_4399b494a9644933edf0e0d7fcb" ON public.post_editors_user USING btree (post_id, user_id);
CREATE INDEX "IDX_9fc34bb51d7a8a50a875dbeff2" ON public.post_editors_user USING btree (post_id);
CREATE INDEX "IDX_4abfcfa0b8814c3522eb488636" ON public.post_editors_user USING btree (user_id);

CREATE TABLE "lecture_editors_user" (
  "lecture_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "lecture_editors_user"
            ADD PRIMARY KEY (lecture_id, user_id);  ALTER TABLE "lecture_editors_user"
            ADD FOREIGN KEY (lecture_id) REFERENCES lecture(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "lecture_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_6cd319be02ecdbfb6b78a3c646c" ON public.lecture_editors_user USING btree (lecture_id, user_id);
CREATE INDEX "IDX_a11c650256fe110cdaa8be2579" ON public.lecture_editors_user USING btree (lecture_id);
CREATE INDEX "IDX_88ce9c7b7ef0b39989593ab150" ON public.lecture_editors_user USING btree (user_id);

CREATE TABLE "chapter_editors_user" (
  "chapter_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "chapter_editors_user"
            ADD PRIMARY KEY (chapter_id, user_id);  ALTER TABLE "chapter_editors_user"
            ADD FOREIGN KEY (chapter_id) REFERENCES chapter(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "chapter_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_6b677152cc31755de3d795224f6" ON public.chapter_editors_user USING btree (chapter_id, user_id);
CREATE INDEX "IDX_6bc76308984eee4bb9c49d9b2a" ON public.chapter_editors_user USING btree (chapter_id);
CREATE INDEX "IDX_22ec2de78ee0f0e2e7ee768a4c" ON public.chapter_editors_user USING btree (user_id);

CREATE TABLE "course_editors_user" (
  "course_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "course_editors_user"
            ADD PRIMARY KEY (course_id, user_id);  ALTER TABLE "course_editors_user"
            ADD FOREIGN KEY (course_id) REFERENCES course(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "course_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_a863e83f726a4e6f0704fcd302d" ON public.course_editors_user USING btree (course_id, user_id);
CREATE INDEX "IDX_f0f27df7244631f69b3aa42a0d" ON public.course_editors_user USING btree (course_id);
CREATE INDEX "IDX_25fff6cb34e063caab1eb4c7c1" ON public.course_editors_user USING btree (user_id);

CREATE TABLE "proposal_editors_user" (
  "proposal_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "proposal_editors_user"
            ADD PRIMARY KEY (proposal_id, user_id);  ALTER TABLE "proposal_editors_user"
            ADD FOREIGN KEY (proposal_id) REFERENCES proposal(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "proposal_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_3fead86d04749b09cebb8fd953d" ON public.proposal_editors_user USING btree (proposal_id, user_id);
CREATE INDEX "IDX_d5efb3b9d5fb4cd25e2f1eb849" ON public.proposal_editors_user USING btree (proposal_id);
CREATE INDEX "IDX_2469607eeb33fa61f32bda5326" ON public.proposal_editors_user USING btree (user_id);

CREATE TABLE "project_editors_user" (
  "project_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "project_editors_user"
            ADD PRIMARY KEY (project_id, user_id);  ALTER TABLE "project_editors_user"
            ADD FOREIGN KEY (project_id) REFERENCES project(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "project_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_efd60535d94affeccba7365e591" ON public.project_editors_user USING btree (project_id, user_id);
CREATE INDEX "IDX_4efce1aaecf2b3d2523959a501" ON public.project_editors_user USING btree (project_id);
CREATE INDEX "IDX_8f5fba1aed12561d33ab675a24" ON public.project_editors_user USING btree (user_id);

CREATE TABLE "ticket_editors_user" (
  "ticket_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "ticket_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "ticket_editors_user"
            ADD PRIMARY KEY (ticket_id, user_id);  ALTER TABLE "ticket_editors_user"
            ADD FOREIGN KEY (ticket_id) REFERENCES ticket(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE INDEX "IDX_68a614c8fd4410c06e03e0fa84" ON public.ticket_editors_user USING btree (user_id);
CREATE UNIQUE INDEX "PK_cbe7f143227c03a658062234a49" ON public.ticket_editors_user USING btree (ticket_id, user_id);
CREATE INDEX "IDX_1d5c90934caa7c88aef8de3798" ON public.ticket_editors_user USING btree (ticket_id);

CREATE TABLE "tag" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "tag" character varying(50) NOT NULL DEFAULT ''::character varying
);

ALTER TABLE "tag"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_8e4052373c579afc1471f526760" ON public.tag USING btree (id);
CREATE UNIQUE INDEX "IDX_9dbf61b2d00d2c77d3b5ced37c" ON public.tag USING btree (tag);

CREATE TABLE "competition_run_submission_report_entity" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "wins_as_p1" integer NOT NULL DEFAULT 0,
  "wins_as_p2" integer NOT NULL DEFAULT 0,
  "total_points" double precision NOT NULL DEFAULT '0'::double precision,
  "run_id" uuid,
  "submission_id" uuid,
  "total_wins" integer NOT NULL DEFAULT 0,
  "points_as_p1" double precision NOT NULL DEFAULT '0'::double precision,
  "points_as_p2" double precision NOT NULL DEFAULT '0'::double precision,
  "user_id" uuid
);

ALTER TABLE "competition_run_submission_report_entity"
            ADD PRIMARY KEY (id);  ALTER TABLE "competition_run_submission_report_entity"
            ADD FOREIGN KEY (run_id) REFERENCES competition_run_entity(id);  ALTER TABLE "competition_run_submission_report_entity"
            ADD FOREIGN KEY (submission_id) REFERENCES competition_submission_entity(id);  ALTER TABLE "competition_run_submission_report_entity"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  CREATE UNIQUE INDEX "PK_efb4ef524197954e35f131fb9ee" ON public.competition_run_submission_report_entity USING btree (id);

CREATE TABLE "competition_run_entity" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "state" character varying NOT NULL DEFAULT 'NOT_STARTED'::character varying,
  "game_type" character varying NOT NULL
);

ALTER TABLE "competition_run_entity"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_c2c36337de9af6a41752324bc01" ON public.competition_run_entity USING btree (id);

CREATE TABLE "competition_match_entity" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "winner" "competition_match_entity_winner_enum",
  "p1_points" double precision NOT NULL,
  "p2_points" double precision NOT NULL,
  "p1_turns" integer NOT NULL,
  "p2_turns" integer NOT NULL,
  "logs" text,
  "run_id" uuid,
  "p1submission_id" uuid,
  "p2submission_id" uuid,
  "last_state" text
);

ALTER TABLE "competition_match_entity"
            ADD PRIMARY KEY (id);  ALTER TABLE "competition_match_entity"
            ADD FOREIGN KEY (run_id) REFERENCES competition_run_entity(id);  ALTER TABLE "competition_match_entity"
            ADD FOREIGN KEY (p1submission_id) REFERENCES competition_submission_entity(id);  ALTER TABLE "competition_match_entity"
            ADD FOREIGN KEY (p2submission_id) REFERENCES competition_submission_entity(id);  CREATE UNIQUE INDEX "PK_c82022b5f020870a48610ec2bce" ON public.competition_match_entity USING btree (id);

CREATE TABLE "competition_submission_entity" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "source_code_zip" bytea NOT NULL,
  "user_id" uuid,
  "game_type" "competition_submission_entity_game_type_enum" NOT NULL,
  "executable" bytea
);

ALTER TABLE "competition_submission_entity"
            ADD PRIMARY KEY (id);  ALTER TABLE "competition_submission_entity"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  CREATE UNIQUE INDEX "PK_6af6334fc1cff3b8b08d2b40cb0" ON public.competition_submission_entity USING btree (id);

CREATE TABLE "user" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "facebook_id" character varying,
  "google_id" character varying,
  "github_id" character varying,
  "apple_id" character varying,
  "linkedin_id" character varying,
  "twitter_id" character varying,
  "wallet_address" character varying,
  "password_hash" character varying,
  "password_salt" character varying,
  "email_verified" boolean NOT NULL DEFAULT false,
  "elo" double precision NOT NULL DEFAULT '400'::double precision,
  "profile_id" uuid,
  "username" character varying(32),
  "email" character varying(254)
);

CREATE UNIQUE INDEX "PK_cace4a159ff9f2512dd42373760" ON public."user" USING btree (id);
CREATE UNIQUE INDEX "UQ_f44d0cd18cfd80b0fed7806c3b7" ON public."user" USING btree (profile_id);
CREATE UNIQUE INDEX "UQ_189473aaba06ffd667bb024e71a" ON public."user" USING btree (facebook_id);
CREATE UNIQUE INDEX "UQ_7adac5c0b28492eb292d4a93871" ON public."user" USING btree (google_id);
CREATE UNIQUE INDEX "UQ_45bb0502759f0dd73c4fd8b13bd" ON public."user" USING btree (github_id);
CREATE UNIQUE INDEX "UQ_fda2d885fb612212b85752f5ab1" ON public."user" USING btree (apple_id);
CREATE UNIQUE INDEX "UQ_f0f25ac3307013265a678db85e4" ON public."user" USING btree (linkedin_id);
CREATE UNIQUE INDEX "UQ_55008adb3b4101af12f495c9c1d" ON public."user" USING btree (twitter_id);
CREATE UNIQUE INDEX "UQ_ac2af862c8540eccb210b293107" ON public."user" USING btree (wallet_address);
CREATE UNIQUE INDEX "UQ_78a916df40e02a9deb1c4b75edb" ON public."user" USING btree (username);
CREATE UNIQUE INDEX "UQ_e12875dfb3b1d92d7d7c5377e22" ON public."user" USING btree (email);
CREATE UNIQUE INDEX "IDX_78a916df40e02a9deb1c4b75ed" ON public."user" USING btree (username);
CREATE UNIQUE INDEX "IDX_e12875dfb3b1d92d7d7c5377e2" ON public."user" USING btree (email);
CREATE UNIQUE INDEX "IDX_189473aaba06ffd667bb024e71" ON public."user" USING btree (facebook_id);
CREATE UNIQUE INDEX "IDX_7adac5c0b28492eb292d4a9387" ON public."user" USING btree (google_id);
CREATE UNIQUE INDEX "IDX_45bb0502759f0dd73c4fd8b13b" ON public."user" USING btree (github_id);
CREATE UNIQUE INDEX "IDX_fda2d885fb612212b85752f5ab" ON public."user" USING btree (apple_id);
CREATE UNIQUE INDEX "IDX_f0f25ac3307013265a678db85e" ON public."user" USING btree (linkedin_id);
CREATE UNIQUE INDEX "IDX_55008adb3b4101af12f495c9c1" ON public."user" USING btree (twitter_id);
CREATE UNIQUE INDEX "IDX_ac2af862c8540eccb210b29310" ON public."user" USING btree (wallet_address);

CREATE TABLE "vote" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now()
);

ALTER TABLE "vote"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_2d5932d46afe39c8176f9d4be72" ON public.vote USING btree (id);

CREATE TABLE "project_feedback_response" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "responses" jsonb NOT NULL,
  "version_id" uuid,
  "user_id" uuid
);

ALTER TABLE "project_feedback_response"
            ADD PRIMARY KEY (id);  ALTER TABLE "project_feedback_response"
            ADD FOREIGN KEY (version_id) REFERENCES project_version(id);  ALTER TABLE "project_feedback_response"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  CREATE UNIQUE INDEX "PK_8ecbac3eb61113faffe1166e11e" ON public.project_feedback_response USING btree (id);

CREATE TABLE "project_version" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "version" character varying(64) NOT NULL,
  "archive_url" character varying(1024) NOT NULL,
  "notes_url" character varying(1024) NOT NULL,
  "feedback_form" jsonb NOT NULL,
  "feedback_deadline" timestamp with time zone NOT NULL,
  "project_id" uuid
);

ALTER TABLE "project_version"
            ADD PRIMARY KEY (id);  ALTER TABLE "project_version"
            ADD UNIQUE (version, project_id);  ALTER TABLE "project_version"
            ADD FOREIGN KEY (project_id) REFERENCES project(id);  CREATE UNIQUE INDEX "PK_249c24f66d8f7e8ea6f9ff462fb" ON public.project_version USING btree (id);
CREATE INDEX "IDX_4b6747e098d96a0e805692810d" ON public.project_version USING btree (feedback_deadline);
CREATE UNIQUE INDEX "UQ_b9103221778444dc9638d3775da" ON public.project_version USING btree (version, project_id);

CREATE TABLE "ticket" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "title" character varying(255) NOT NULL,
  "description" text,
  "status" "ticket_status_enum" NOT NULL DEFAULT 'OPEN'::ticket_status_enum,
  "priority" "ticket_priority_enum" NOT NULL DEFAULT 'LOW'::ticket_priority_enum,
  "owner_id" uuid,
  "project_id" uuid
);

ALTER TABLE "ticket"
            ADD FOREIGN KEY (project_id) REFERENCES project(id);  ALTER TABLE "ticket"
            ADD PRIMARY KEY (id);  ALTER TABLE "ticket"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  CREATE UNIQUE INDEX "PK_d9a0835407701eb86f874474b7c" ON public.ticket USING btree (id);

CREATE TABLE "user_profile" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "bio" character varying(256),
  "name" character varying(256),
  "given_name" character varying(256),
  "family_name" character varying(256),
  "picture_id" uuid
);

ALTER TABLE "user_profile"
            ADD PRIMARY KEY (id);  ALTER TABLE "user_profile"
            ADD UNIQUE (picture_id);  ALTER TABLE "user_profile"
            ADD FOREIGN KEY (picture_id) REFERENCES images(id);  CREATE UNIQUE INDEX "PK_f44d0cd18cfd80b0fed7806c3b7" ON public.user_profile USING btree (id);
CREATE UNIQUE INDEX "UQ_e4238c3828bc51ff8ca27c46385" ON public.user_profile USING btree (picture_id);

CREATE TABLE "job_post_job_tags_job_tag" (
  "job_post_id" uuid NOT NULL,
  "job_tag_id" uuid NOT NULL
);

ALTER TABLE "job_post_job_tags_job_tag"
            ADD PRIMARY KEY (job_post_id, job_tag_id);  ALTER TABLE "job_post_job_tags_job_tag"
            ADD FOREIGN KEY (job_post_id) REFERENCES job_post(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_job_tags_job_tag"
            ADD FOREIGN KEY (job_tag_id) REFERENCES job_tag(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_0a636facbab11355e4f4bbbf3f3" ON public.job_post_job_tags_job_tag USING btree (job_post_id, job_tag_id);
CREATE INDEX "IDX_bce26f11d48dfe6a441bb8d8a0" ON public.job_post_job_tags_job_tag USING btree (job_post_id);
CREATE INDEX "IDX_22911465a689416e80716524bd" ON public.job_post_job_tags_job_tag USING btree (job_tag_id);

CREATE TABLE "post" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "visibility" "post_visibility_enum" NOT NULL DEFAULT 'DRAFT'::post_visibility_enum,
  "post_type" "post_post_type_enum" NOT NULL DEFAULT 'BLOG'::post_post_type_enum,
  "body" text,
  "owner_id" uuid,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid
);

ALTER TABLE "post"
            ADD PRIMARY KEY (id);  ALTER TABLE "post"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "post"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  CREATE UNIQUE INDEX "PK_be5fda3aac270b134ff9c21cdee" ON public.post USING btree (id);
CREATE INDEX "IDX_a80ca3bf4ca3711c488cb82cf7" ON public.post USING btree (visibility);
CREATE UNIQUE INDEX "IDX_cd1bddce36edc3e766798eab37" ON public.post USING btree (slug);
CREATE INDEX "IDX_e28aa0c4114146bfb1567bfa9a" ON public.post USING btree (title);

CREATE TABLE "chapter" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "visibility" "chapter_visibility_enum" NOT NULL DEFAULT 'DRAFT'::chapter_visibility_enum,
  "order" double precision NOT NULL DEFAULT '0'::double precision,
  "course_id" uuid,
  "body" text,
  "owner_id" uuid,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid
);

ALTER TABLE "chapter"
            ADD PRIMARY KEY (id);  ALTER TABLE "chapter"
            ADD FOREIGN KEY (course_id) REFERENCES course(id);  ALTER TABLE "chapter"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "chapter"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  CREATE UNIQUE INDEX "PK_275bd1c62bed7dff839680614ca" ON public.chapter USING btree (id);
CREATE INDEX "IDX_859d333ca13d040bf2f941afcd" ON public.chapter USING btree (visibility);
CREATE INDEX "IDX_2c985baf03bef490c97e168cdc" ON public.chapter USING btree ("order");
CREATE UNIQUE INDEX "IDX_0c24da73936f0eb347e9835b4f" ON public.chapter USING btree (slug);
CREATE INDEX "IDX_1540f666b7a73b75b2c13e5db2" ON public.chapter USING btree (title);

CREATE TABLE "course" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "visibility" "course_visibility_enum" NOT NULL DEFAULT 'DRAFT'::course_visibility_enum,
  "subscription_access" boolean NOT NULL DEFAULT true,
  "body" text,
  "price" numeric DEFAULT '0'::numeric,
  "owner_id" uuid,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid
);

ALTER TABLE "course"
            ADD PRIMARY KEY (id);  ALTER TABLE "course"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "course"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  CREATE UNIQUE INDEX "PK_bf95180dd756fd204fb01ce4916" ON public.course USING btree (id);
CREATE INDEX "IDX_653293097239013a0e49f481f9" ON public.course USING btree (visibility);
CREATE INDEX "IDX_399d4e499683d688c07a41da26" ON public.course USING btree (subscription_access);
CREATE UNIQUE INDEX "IDX_a101f48e5045bcf501540a4a5b" ON public.course USING btree (slug);
CREATE INDEX "IDX_4f1c8311d8e665e0d0c0046326" ON public.course USING btree (price);
CREATE INDEX "IDX_ac5edecc1aefa58ed0237a7ee4" ON public.course USING btree (title);

CREATE TABLE "proposal" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "visibility" "proposal_visibility_enum" NOT NULL DEFAULT 'DRAFT'::proposal_visibility_enum,
  "body" text,
  "owner_id" uuid,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid
);

ALTER TABLE "proposal"
            ADD PRIMARY KEY (id);  ALTER TABLE "proposal"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "proposal"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  CREATE UNIQUE INDEX "PK_ca872ecfe4fef5720d2d39e4275" ON public.proposal USING btree (id);
CREATE INDEX "IDX_f0f2c5e15690f642f3afcde829" ON public.proposal USING btree (visibility);
CREATE UNIQUE INDEX "IDX_894de7603d8172deb20277c6f5" ON public.proposal USING btree (slug);
CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON public.proposal USING btree (title);

CREATE TABLE "project" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "body" text,
  "visibility" "project_visibility_enum" NOT NULL DEFAULT 'DRAFT'::project_visibility_enum,
  "owner_id" uuid,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid,
  "banner_id" uuid
);

ALTER TABLE "project"
            ADD PRIMARY KEY (id);  ALTER TABLE "project"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "project"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "project"
            ADD FOREIGN KEY (banner_id) REFERENCES images(id) ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_4d68b1358bb5b766d3e78f32f57" ON public.project USING btree (id);
CREATE UNIQUE INDEX "IDX_6fce32ddd71197807027be6ad3" ON public.project USING btree (slug);
CREATE INDEX "IDX_0c2943d87d6b4f22558a7fce71" ON public.project USING btree (visibility);
CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON public.project USING btree (title);

CREATE TABLE "project_screenshots_images" (
  "project_id" uuid NOT NULL,
  "images_id" uuid NOT NULL
);

ALTER TABLE "project_screenshots_images"
            ADD PRIMARY KEY (project_id, images_id);  ALTER TABLE "project_screenshots_images"
            ADD FOREIGN KEY (project_id) REFERENCES project(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "project_screenshots_images"
            ADD FOREIGN KEY (images_id) REFERENCES images(id) ON UPDATE CASCADE ON DELETE CASCADE;  CREATE UNIQUE INDEX "PK_2e301330d17fb5c8f1695a51e39" ON public.project_screenshots_images USING btree (project_id, images_id);
CREATE INDEX "IDX_4e1f8d02befe1e5a392910b624" ON public.project_screenshots_images USING btree (project_id);
CREATE INDEX "IDX_ef0ec5b9da603747097dea89a4" ON public.project_screenshots_images USING btree (images_id);

CREATE TABLE "images" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "source" character varying NOT NULL,
  "path" character varying NOT NULL DEFAULT ''::character varying,
  "filename" character varying(255) NOT NULL DEFAULT ''::character varying,
  "mimetype" character varying(255) NOT NULL DEFAULT ''::character varying,
  "size_bytes" integer NOT NULL DEFAULT 0,
  "hash" character varying NOT NULL,
  "width" integer,
  "height" integer,
  "description" character varying,
  "original_filename" character varying NOT NULL DEFAULT ''::character varying
);

ALTER TABLE "images"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_1fe148074c6a1a91b63cb9ee3c9" ON public.images USING btree (id);
CREATE INDEX "IDX_c738d9c7c4c56992bb623c88e2" ON public.images USING btree (source);
CREATE INDEX "IDX_3fed0dc195b842723edad36ada" ON public.images USING btree (filename);
CREATE INDEX "IDX_b2900054b2cdd0b2035839335e" ON public.images USING btree (mimetype);
CREATE INDEX "IDX_11841db41fa6b76f3c6f431ed3" ON public.images USING btree (hash);
CREATE INDEX "IDX_9cb6c0392eebfa05570c5b3958" ON public.images USING btree (width);
CREATE INDEX "IDX_afbfec6622f2ac33dc8ee3125f" ON public.images USING btree (height);
CREATE INDEX "IDX_b27820f9c4eb00f2afc4e5b616" ON public.images USING btree (path);
CREATE INDEX "IDX_382daa5c2a55e77f5a960012ac" ON public.images USING btree (original_filename);
CREATE UNIQUE INDEX "pathUniqueness" ON public.images USING btree (path, filename, source);

CREATE TABLE "lecture" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "visibility" "lecture_visibility_enum" NOT NULL DEFAULT 'DRAFT'::lecture_visibility_enum,
  "order" double precision NOT NULL DEFAULT '0'::double precision,
  "course_id" uuid,
  "chapter_id" uuid,
  "body" text,
  "owner_id" uuid,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid,
  "renderer" "lecture_renderer_enum" NOT NULL DEFAULT 'markdown'::lecture_renderer_enum
);

ALTER TABLE "lecture"
            ADD PRIMARY KEY (id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (course_id) REFERENCES course(id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (chapter_id) REFERENCES chapter(id);  CREATE UNIQUE INDEX "PK_2abef7c1e52b7b58a9f905c9643" ON public.lecture USING btree (id);
CREATE INDEX "IDX_f5cadec277927351eb00098dd7" ON public.lecture USING btree (visibility);
CREATE INDEX "IDX_c6c3264ce6966d5c1d078e147b" ON public.lecture USING btree ("order");
CREATE UNIQUE INDEX "IDX_dff3c45c6a2688bdef948f158a" ON public.lecture USING btree (slug);
CREATE INDEX "IDX_4c8c4ea44b8a5a5c6a028b9e20" ON public.lecture USING btree (title);
CREATE INDEX "IDX_d316a6a4d94b0b4aaab3af3f31" ON public.lecture USING btree (renderer);

CREATE TABLE "job_post_editors_user" (
  "job_post_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "job_post_editors_user"
            ADD FOREIGN KEY (job_post_id) REFERENCES job_post(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_editors_user"
            ADD PRIMARY KEY (job_post_id, user_id);  CREATE INDEX "IDX_d41a469fa70f6e342f137a61d0" ON public.job_post_editors_user USING btree (job_post_id);
CREATE INDEX "IDX_332380fd9f01b6e74b2509f5f6" ON public.job_post_editors_user USING btree (user_id);
CREATE UNIQUE INDEX "PK_b2de4693be0a04ac6bae0e82544" ON public.job_post_editors_user USING btree (job_post_id, user_id);

CREATE TABLE "job_tag" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "name" character varying(256) NOT NULL
);

ALTER TABLE "job_tag"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_0c4e9330020ee70d765dff0486" ON public.job_tag USING btree (name);
CREATE UNIQUE INDEX "PK_835586a09d10e323fdec92aaaa1" ON public.job_tag USING btree (id);

CREATE TABLE "job_post" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" character varying(1024),
  "body" text,
  "visibility" "job_post_visibility_enum" NOT NULL DEFAULT 'DRAFT'::job_post_visibility_enum,
  "owner_id" uuid,
  "location" character varying(256) NOT NULL DEFAULT 'Remote'::character varying,
  "job_type" "job_post_job_type_enum" NOT NULL DEFAULT 'TASK'::job_post_job_type_enum,
  "title" character varying(255) NOT NULL,
  "thumbnail_id" uuid
);

ALTER TABLE "job_post"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "job_post"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "job_post"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "IDX_902620afcf0f13d981154aac83" ON public.job_post USING btree (slug);
CREATE INDEX "IDX_ee26d130dc420c2e35f42573ce" ON public.job_post USING btree (title);
CREATE INDEX "IDX_03b7a2e0ae0f87dae10d1e9f00" ON public.job_post USING btree (visibility);
CREATE INDEX "IDX_ec0b3c542afe53891e30476936" ON public.job_post USING btree (location);
CREATE INDEX "IDX_f8c69a1e815225cdd8a30de66b" ON public.job_post USING btree (job_type);
CREATE UNIQUE INDEX "PK_19c6a7e604f32663d470210610b" ON public.job_post USING btree (id);

CREATE TABLE "job_application" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "progress" integer NOT NULL DEFAULT 0,
  "applicant_id" uuid NOT NULL,
  "job_id" uuid,
  "rejected" boolean NOT NULL DEFAULT false,
  "withdrawn" boolean NOT NULL DEFAULT false
);

ALTER TABLE "job_application"
            ADD FOREIGN KEY (applicant_id) REFERENCES "user"(id);  ALTER TABLE "job_application"
            ADD FOREIGN KEY (job_id) REFERENCES job_post(id);  ALTER TABLE "job_application"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_896c8c02d7da2c0228d586e54b4" ON public.job_application USING btree (id);

