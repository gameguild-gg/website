CREATE TYPE "certificate_tags_type_enum" AS ENUM ('requirement', 'recommendation', 'provides');

CREATE TYPE "certificates_certificate_type_enum" AS ENUM ('program_completion', 'product_bundle_completion', 'learning_pathway', 'skill_mastery', 'event_participation', 'assessment_passed', 'project_completion', 'specialization', 'professional', 'achievement', 'instructor', 'time_investment', 'peer_recognition');

CREATE TYPE "certificates_certificate_verification_method_enum" AS ENUM ('code', 'blockchain', 'both');

CREATE TYPE "chapter_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "competition_match_entity_winner_enum" AS ENUM ('Player1', 'Player2');

CREATE TYPE "competition_submission_entity_game_type_enum" AS ENUM ('CatchTheCat', 'Chess');

CREATE TYPE "content_interactions_status_enum" AS ENUM ('not_started', 'in_progress', 'completed', 'skipped');

CREATE TYPE "course_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "financial_transactions_status_enum" AS ENUM ('pending', 'processing', 'completed', 'failed', 'refunded', 'cancelled');

CREATE TYPE "financial_transactions_transaction_type_enum" AS ENUM ('purchase', 'refund', 'withdrawal', 'deposit', 'transfer', 'fee', 'adjustment');

CREATE TYPE "game_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "job_post_job_type_enum" AS ENUM ('CONTINUOUS', 'TASK');

CREATE TYPE "job_post_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "lecture_renderer_enum" AS ENUM ('markdown', 'youtube', 'lexical', 'reveal', 'html', 'pdf', 'image', 'video', 'audio', 'code', 'link');

CREATE TYPE "lecture_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "post_post_type_enum" AS ENUM ('BLOG');

CREATE TYPE "post_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "product_subscription_plans_billing_interval_enum" AS ENUM ('day', 'week', 'month', 'year');

CREATE TYPE "product_subscription_plans_type_enum" AS ENUM ('monthly', 'quarterly', 'annual', 'lifetime');

CREATE TYPE "products_type_enum" AS ENUM ('program', 'learning_pathway', 'bundle', 'subscription', 'workshop', 'mentorship', 'ebook', 'resource_pack', 'community', 'certification', 'other');

CREATE TYPE "products_visibility_enum" AS ENUM ('draft', 'published', 'archived');

CREATE TYPE "program_contents_grading_method_enum" AS ENUM ('instructor', 'peer', 'ai', 'automated_tests');

CREATE TYPE "program_contents_type_enum" AS ENUM ('page', 'assignment', 'questionnaire', 'discussion', 'code', 'challenge', 'reflection', 'survey');

CREATE TYPE "program_ratings_moderation_status_enum" AS ENUM ('pending', 'approved', 'rejected', 'flagged');

CREATE TYPE "program_user_roles_role_enum" AS ENUM ('student', 'instructor', 'editor', 'administrator', 'teaching_assistant');

CREATE TYPE "project_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "promo_codes_type_enum" AS ENUM ('percentage_off', 'fixed_amount_off', 'buy_one_get_one', 'first_month_free');

CREATE TYPE "proposal_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "quiz_visibility_enum" AS ENUM ('DRAFT', 'PUBLISHED', 'FUTURE', 'PENDING', 'PRIVATE', 'TRASH');

CREATE TYPE "tag_proficiencies_proficiency_level_enum" AS ENUM ('awareness', 'novice', 'beginner', 'intermediate', 'advanced', 'expert', 'master');

CREATE TYPE "tag_relationships_type_enum" AS ENUM ('related', 'parent', 'child', 'requires', 'suggested');

CREATE TYPE "tags_type_enum" AS ENUM ('skill', 'topic', 'technology', 'difficulty', 'category', 'industry', 'certification');

CREATE TYPE "ticket_priority_enum" AS ENUM ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL');

CREATE TYPE "ticket_status_enum" AS ENUM ('OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED');

CREATE TYPE "user_certificates_status_enum" AS ENUM ('active', 'expired', 'revoked', 'pending');

CREATE TYPE "user_financial_methods_method_type_enum" AS ENUM ('credit_card', 'debit_card', 'crypto_wallet', 'wallet_balance', 'bank_transfer');

CREATE TYPE "user_financial_methods_status_enum" AS ENUM ('active', 'inactive', 'expired', 'removed');

CREATE TYPE "user_kyc_verifications_provider_enum" AS ENUM ('sumsub', 'shufti', 'onfido', 'jumio', 'custom');

CREATE TYPE "user_kyc_verifications_status_enum" AS ENUM ('pending', 'in_progress', 'approved', 'rejected', 'suspended', 'expired');

CREATE TYPE "user_products_acquisition_type_enum" AS ENUM ('purchase', 'subscription', 'free', 'gift');

CREATE TYPE "user_products_status_enum" AS ENUM ('active', 'expired', 'revoked', 'suspended');

CREATE TYPE "user_subscriptions_status_enum" AS ENUM ('active', 'trialing', 'past_due', 'canceled', 'incomplete', 'incomplete_expired', 'unpaid');

CREATE TABLE "activity_grades" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "grade" numeric,
  "graded_at" timestamp without time zone,
  "feedback" text,
  "rubric_assessment" jsonb,
  "metadata" jsonb,
  "content_interaction_id" uuid,
  "grader_program_user_id" uuid
);

ALTER TABLE "activity_grades"
            ADD FOREIGN KEY (content_interaction_id) REFERENCES content_interactions(id) ON DELETE CASCADE;  ALTER TABLE "activity_grades"
            ADD FOREIGN KEY (grader_program_user_id) REFERENCES program_users(id);  ALTER TABLE "activity_grades"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "IDX_5f550ee6239b8f4b0931ecc938" ON public.activity_grades USING btree (content_interaction_id);
CREATE UNIQUE INDEX "PK_fa586cf477e73abb88e12e87f4a" ON public.activity_grades USING btree (id);

CREATE TABLE "certificate_blockchain_anchors" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "blockchain_network" character varying NOT NULL,
  "transaction_hash" character varying NOT NULL,
  "block_number" bigint NOT NULL,
  "smart_contract_address" character varying,
  "token_id" character varying,
  "anchored_at" timestamp without time zone NOT NULL,
  "public_verification_url" character varying,
  "ipfs_hash" character varying,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "certificate_id" uuid NOT NULL
);

ALTER TABLE "certificate_blockchain_anchors"
            ADD FOREIGN KEY (certificate_id) REFERENCES user_certificates(id);  ALTER TABLE "certificate_blockchain_anchors"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_0033f7bb83698a13a8914b9c66" ON public.certificate_blockchain_anchors USING btree (blockchain_network);
CREATE INDEX "IDX_283d4b4842f0e3a7953c5b0f1b" ON public.certificate_blockchain_anchors USING btree (token_id);
CREATE INDEX "IDX_3291395ce689c51d103f8b6e09" ON public.certificate_blockchain_anchors USING btree (certificate_id);
CREATE INDEX "IDX_37fc52ce06b6bc0a18d605186a" ON public.certificate_blockchain_anchors USING btree (transaction_hash);
CREATE UNIQUE INDEX "PK_64e844852c45801b2a3529e049f" ON public.certificate_blockchain_anchors USING btree (id);

CREATE TABLE "certificate_tags" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "type" "certificate_tags_type_enum" NOT NULL,
  "metadata" jsonb,
  "certificate_id" uuid,
  "tag_id" uuid
);

ALTER TABLE "certificate_tags"
            ADD FOREIGN KEY (tag_id) REFERENCES tag_proficiencies(id) ON DELETE CASCADE;  ALTER TABLE "certificate_tags"
            ADD FOREIGN KEY (certificate_id) REFERENCES certificates(id) ON DELETE CASCADE;  ALTER TABLE "certificate_tags"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_5d34b1014c933c6dc8da5764c08" ON public.certificate_tags USING btree (id);

CREATE TABLE "certificates" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "certificate_type" "certificates_certificate_type_enum" NOT NULL DEFAULT 'program_completion'::certificates_certificate_type_enum,
  "name" character varying(255) NOT NULL,
  "description" text NOT NULL,
  "html_template" text NOT NULL,
  "css_styles" text NOT NULL,
  "auto_issue" boolean NOT NULL DEFAULT true,
  "minimum_grade" double precision NOT NULL DEFAULT '70'::double precision,
  "completion_percentage" integer NOT NULL DEFAULT 100,
  "requires_feedback" boolean NOT NULL DEFAULT false,
  "requires_rating" boolean NOT NULL DEFAULT false,
  "minimum_rating" double precision,
  "feedback_form_template" jsonb,
  "expiration_months" integer,
  "certificate_verification_method" "certificates_certificate_verification_method_enum" NOT NULL DEFAULT 'code'::certificates_certificate_verification_method_enum,
  "prerequisites" jsonb,
  "badge_image" character varying,
  "signature_image" character varying,
  "credential_title" character varying,
  "issuer_name" character varying,
  "metadata" jsonb,
  "is_active" boolean NOT NULL DEFAULT true,
  "deleted_at" timestamp without time zone,
  "program_id" uuid,
  "product_id" uuid
);

ALTER TABLE "certificates"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;  ALTER TABLE "certificates"
            ADD FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;  ALTER TABLE "certificates"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_1eb0d8d2840d24d211ac1e29b7" ON public.certificates USING btree (program_id);
CREATE INDEX "IDX_82c9eb5da92f6eb4b1a0f285fe" ON public.certificates USING btree (certificate_type);
CREATE INDEX "IDX_9e25120646f90fd0815171dba0" ON public.certificates USING btree (product_id);
CREATE INDEX "IDX_d5cdecac88236f3c3a3e1a2205" ON public.certificates USING btree (is_active);
CREATE UNIQUE INDEX "PK_e4c7e31e2144300bea7d89eb165" ON public.certificates USING btree (id);

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
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "chapter"
            ADD FOREIGN KEY (course_id) REFERENCES course(id);  ALTER TABLE "chapter"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "chapter"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_1540f666b7a73b75b2c13e5db2" ON public.chapter USING btree (title);
CREATE INDEX "IDX_2c985baf03bef490c97e168cdc" ON public.chapter USING btree ("order");
CREATE INDEX "IDX_859d333ca13d040bf2f941afcd" ON public.chapter USING btree (visibility);
CREATE UNIQUE INDEX "IDX_0c24da73936f0eb347e9835b4f" ON public.chapter USING btree (slug);
CREATE UNIQUE INDEX "PK_275bd1c62bed7dff839680614ca" ON public.chapter USING btree (id);

CREATE TABLE "chapter_editors_user" (
  "chapter_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "chapter_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "chapter_editors_user"
            ADD FOREIGN KEY (chapter_id) REFERENCES chapter(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "chapter_editors_user"
            ADD PRIMARY KEY (chapter_id, user_id);  CREATE INDEX "IDX_22ec2de78ee0f0e2e7ee768a4c" ON public.chapter_editors_user USING btree (user_id);
CREATE INDEX "IDX_6bc76308984eee4bb9c49d9b2a" ON public.chapter_editors_user USING btree (chapter_id);
CREATE UNIQUE INDEX "PK_6b677152cc31755de3d795224f6" ON public.chapter_editors_user USING btree (chapter_id, user_id);

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
  "last_state" text,
  "p1cpu_time" real NOT NULL DEFAULT '0'::real,
  "p2cpu_time" real NOT NULL DEFAULT '0'::real
);

ALTER TABLE "competition_match_entity"
            ADD FOREIGN KEY (p1submission_id) REFERENCES competition_submission_entity(id);  ALTER TABLE "competition_match_entity"
            ADD FOREIGN KEY (run_id) REFERENCES competition_run_entity(id);  ALTER TABLE "competition_match_entity"
            ADD FOREIGN KEY (p2submission_id) REFERENCES competition_submission_entity(id);  ALTER TABLE "competition_match_entity"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_c82022b5f020870a48610ec2bce" ON public.competition_match_entity USING btree (id);

CREATE TABLE "competition_run_entity" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "state" character varying NOT NULL DEFAULT 'NOT_STARTED'::character varying,
  "game_type" character varying NOT NULL
);

ALTER TABLE "competition_run_entity"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_c2c36337de9af6a41752324bc01" ON public.competition_run_entity USING btree (id);

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
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  ALTER TABLE "competition_run_submission_report_entity"
            ADD FOREIGN KEY (run_id) REFERENCES competition_run_entity(id);  ALTER TABLE "competition_run_submission_report_entity"
            ADD FOREIGN KEY (submission_id) REFERENCES competition_submission_entity(id);  ALTER TABLE "competition_run_submission_report_entity"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_efb4ef524197954e35f131fb9ee" ON public.competition_run_submission_report_entity USING btree (id);

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
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  ALTER TABLE "competition_submission_entity"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_6af6334fc1cff3b8b08d2b40cb0" ON public.competition_submission_entity USING btree (id);

CREATE TABLE "content_interactions" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "status" "content_interactions_status_enum" NOT NULL DEFAULT 'not_started'::content_interactions_status_enum,
  "started_at" timestamp without time zone,
  "completed_at" timestamp without time zone,
  "time_spent_seconds" integer NOT NULL DEFAULT 0,
  "last_accessed_at" timestamp without time zone,
  "submitted_at" timestamp without time zone,
  "answers" jsonb,
  "text_response" text,
  "url_response" character varying,
  "file_response" jsonb,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "program_user_id" uuid,
  "content_id" uuid
);

ALTER TABLE "content_interactions"
            ADD FOREIGN KEY (content_id) REFERENCES program_contents(id) ON DELETE CASCADE;  ALTER TABLE "content_interactions"
            ADD FOREIGN KEY (program_user_id) REFERENCES program_users(id) ON DELETE CASCADE;  ALTER TABLE "content_interactions"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_39e13bc2b7fb5360fe2ecc30de" ON public.content_interactions USING btree (program_user_id, status);
CREATE INDEX "IDX_4ee189ef0ebab2ab5645ddb880" ON public.content_interactions USING btree (content_id, status);
CREATE INDEX "IDX_6b25f0f08b688b0e17ac97793a" ON public.content_interactions USING btree (submitted_at);
CREATE INDEX "IDX_eccd0e268fd973ba96803d0e6f" ON public.content_interactions USING btree (completed_at);
CREATE UNIQUE INDEX "IDX_a86ba3342df6bca2e02ff903ad" ON public.content_interactions USING btree (program_user_id, content_id);
CREATE UNIQUE INDEX "PK_4eb661a3ed146d6cc809100e7dd" ON public.content_interactions USING btree (id);

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
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "course"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "course"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_399d4e499683d688c07a41da26" ON public.course USING btree (subscription_access);
CREATE INDEX "IDX_4f1c8311d8e665e0d0c0046326" ON public.course USING btree (price);
CREATE INDEX "IDX_653293097239013a0e49f481f9" ON public.course USING btree (visibility);
CREATE INDEX "IDX_ac5edecc1aefa58ed0237a7ee4" ON public.course USING btree (title);
CREATE UNIQUE INDEX "IDX_a101f48e5045bcf501540a4a5b" ON public.course USING btree (slug);
CREATE UNIQUE INDEX "PK_bf95180dd756fd204fb01ce4916" ON public.course USING btree (id);

CREATE TABLE "course_editors_user" (
  "course_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "course_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "course_editors_user"
            ADD FOREIGN KEY (course_id) REFERENCES course(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "course_editors_user"
            ADD PRIMARY KEY (course_id, user_id);  CREATE INDEX "IDX_25fff6cb34e063caab1eb4c7c1" ON public.course_editors_user USING btree (user_id);
CREATE INDEX "IDX_f0f27df7244631f69b3aa42a0d" ON public.course_editors_user USING btree (course_id);
CREATE UNIQUE INDEX "PK_a863e83f726a4e6f0704fcd302d" ON public.course_editors_user USING btree (course_id, user_id);

CREATE TABLE "financial_transactions" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "transaction_type" "financial_transactions_transaction_type_enum" NOT NULL,
  "amount" numeric NOT NULL,
  "original_amount" numeric NOT NULL,
  "referral_commission_amount" numeric,
  "status" "financial_transactions_status_enum" NOT NULL,
  "payment_provider" character varying(255) NOT NULL,
  "payment_provider_transaction_id" character varying(255) NOT NULL,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "from_user_id" uuid,
  "to_user_id" uuid,
  "product_id" uuid,
  "pricing_id" uuid,
  "subscription_plan_id" uuid,
  "promo_code_id" uuid,
  "referrer_user_id" uuid
);

ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (product_id) REFERENCES products(id);  ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (pricing_id) REFERENCES product_pricing(id);  ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (subscription_plan_id) REFERENCES product_subscription_plans(id);  ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (from_user_id) REFERENCES "user"(id);  ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (referrer_user_id) REFERENCES "user"(id);  ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (promo_code_id) REFERENCES promo_codes(id);  ALTER TABLE "financial_transactions"
            ADD FOREIGN KEY (to_user_id) REFERENCES "user"(id);  ALTER TABLE "financial_transactions"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_0714f08a777d81a139cf7bbf5e" ON public.financial_transactions USING btree (transaction_type);
CREATE INDEX "IDX_09bfda2980f0e5a353b8a1a6fc" ON public.financial_transactions USING btree (product_id);
CREATE INDEX "IDX_31d48836ca680ca1c8e7d5f655" ON public.financial_transactions USING btree (created_at);
CREATE INDEX "IDX_5e1d060cfea925ab38f6edadbc" ON public.financial_transactions USING btree (status);
CREATE INDEX "IDX_70a343d1aa87998fff00092d47" ON public.financial_transactions USING btree (from_user_id);
CREATE INDEX "IDX_9a1240d5ca2fcce9854367fac3" ON public.financial_transactions USING btree (payment_provider_transaction_id);
CREATE INDEX "IDX_cfae546547cae2739baf60206d" ON public.financial_transactions USING btree (referrer_user_id);
CREATE INDEX "IDX_f0c62234bbde401a4f11a9af3e" ON public.financial_transactions USING btree (to_user_id);
CREATE UNIQUE INDEX "PK_3f0ffe3ca2def8783ad8bb5036b" ON public.financial_transactions USING btree (id);

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
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_11841db41fa6b76f3c6f431ed3" ON public.images USING btree (hash);
CREATE INDEX "IDX_382daa5c2a55e77f5a960012ac" ON public.images USING btree (original_filename);
CREATE INDEX "IDX_3fed0dc195b842723edad36ada" ON public.images USING btree (filename);
CREATE INDEX "IDX_9cb6c0392eebfa05570c5b3958" ON public.images USING btree (width);
CREATE INDEX "IDX_afbfec6622f2ac33dc8ee3125f" ON public.images USING btree (height);
CREATE INDEX "IDX_b27820f9c4eb00f2afc4e5b616" ON public.images USING btree (path);
CREATE INDEX "IDX_b2900054b2cdd0b2035839335e" ON public.images USING btree (mimetype);
CREATE INDEX "IDX_c738d9c7c4c56992bb623c88e2" ON public.images USING btree (source);
CREATE UNIQUE INDEX "pathUniqueness" ON public.images USING btree (path, filename, source);
CREATE UNIQUE INDEX "PK_1fe148074c6a1a91b63cb9ee3c9" ON public.images USING btree (id);

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
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_03b7a2e0ae0f87dae10d1e9f00" ON public.job_post USING btree (visibility);
CREATE INDEX "IDX_ec0b3c542afe53891e30476936" ON public.job_post USING btree (location);
CREATE INDEX "IDX_ee26d130dc420c2e35f42573ce" ON public.job_post USING btree (title);
CREATE INDEX "IDX_f8c69a1e815225cdd8a30de66b" ON public.job_post USING btree (job_type);
CREATE UNIQUE INDEX "IDX_902620afcf0f13d981154aac83" ON public.job_post USING btree (slug);
CREATE UNIQUE INDEX "PK_19c6a7e604f32663d470210610b" ON public.job_post USING btree (id);

CREATE TABLE "job_post_editors_user" (
  "job_post_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "job_post_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_editors_user"
            ADD FOREIGN KEY (job_post_id) REFERENCES job_post(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_editors_user"
            ADD PRIMARY KEY (job_post_id, user_id);  CREATE INDEX "IDX_332380fd9f01b6e74b2509f5f6" ON public.job_post_editors_user USING btree (user_id);
CREATE INDEX "IDX_d41a469fa70f6e342f137a61d0" ON public.job_post_editors_user USING btree (job_post_id);
CREATE UNIQUE INDEX "PK_b2de4693be0a04ac6bae0e82544" ON public.job_post_editors_user USING btree (job_post_id, user_id);

CREATE TABLE "job_post_job_tags_job_tag" (
  "job_post_id" uuid NOT NULL,
  "job_tag_id" uuid NOT NULL
);

ALTER TABLE "job_post_job_tags_job_tag"
            ADD FOREIGN KEY (job_tag_id) REFERENCES job_tag(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_job_tags_job_tag"
            ADD FOREIGN KEY (job_post_id) REFERENCES job_post(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "job_post_job_tags_job_tag"
            ADD PRIMARY KEY (job_post_id, job_tag_id);  CREATE INDEX "IDX_22911465a689416e80716524bd" ON public.job_post_job_tags_job_tag USING btree (job_tag_id);
CREATE INDEX "IDX_bce26f11d48dfe6a441bb8d8a0" ON public.job_post_job_tags_job_tag USING btree (job_post_id);
CREATE UNIQUE INDEX "PK_0a636facbab11355e4f4bbbf3f3" ON public.job_post_job_tags_job_tag USING btree (job_post_id, job_tag_id);

CREATE TABLE "job_tag" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "name" character varying(256) NOT NULL
);

ALTER TABLE "job_tag"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_0c4e9330020ee70d765dff0486" ON public.job_tag USING btree (name);
CREATE UNIQUE INDEX "PK_835586a09d10e323fdec92aaaa1" ON public.job_tag USING btree (id);

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
  "renderer" "lecture_renderer_enum" NOT NULL DEFAULT 'markdown'::lecture_renderer_enum,
  "json" jsonb
);

ALTER TABLE "lecture"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (course_id) REFERENCES course(id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "lecture"
            ADD FOREIGN KEY (chapter_id) REFERENCES chapter(id);  ALTER TABLE "lecture"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_4c8c4ea44b8a5a5c6a028b9e20" ON public.lecture USING btree (title);
CREATE INDEX "IDX_c6c3264ce6966d5c1d078e147b" ON public.lecture USING btree ("order");
CREATE INDEX "IDX_d316a6a4d94b0b4aaab3af3f31" ON public.lecture USING btree (renderer);
CREATE INDEX "IDX_f5cadec277927351eb00098dd7" ON public.lecture USING btree (visibility);
CREATE UNIQUE INDEX "IDX_dff3c45c6a2688bdef948f158a" ON public.lecture USING btree (slug);
CREATE UNIQUE INDEX "PK_2abef7c1e52b7b58a9f905c9643" ON public.lecture USING btree (id);

CREATE TABLE "lecture_editors_user" (
  "lecture_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "lecture_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "lecture_editors_user"
            ADD FOREIGN KEY (lecture_id) REFERENCES lecture(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "lecture_editors_user"
            ADD PRIMARY KEY (lecture_id, user_id);  CREATE INDEX "IDX_88ce9c7b7ef0b39989593ab150" ON public.lecture_editors_user USING btree (user_id);
CREATE INDEX "IDX_a11c650256fe110cdaa8be2579" ON public.lecture_editors_user USING btree (lecture_id);
CREATE UNIQUE INDEX "PK_6cd319be02ecdbfb6b78a3c646c" ON public.lecture_editors_user USING btree (lecture_id, user_id);

CREATE TABLE "migrations" (
  "id" integer NOT NULL DEFAULT nextval('migrations_id_seq'::regclass),
  "timestamp" bigint NOT NULL,
  "name" character varying NOT NULL
);

ALTER TABLE "migrations"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_8c82d7f526340ab734260ea46be" ON public.migrations USING btree (id);

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
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "post"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "post"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_a80ca3bf4ca3711c488cb82cf7" ON public.post USING btree (visibility);
CREATE INDEX "IDX_e28aa0c4114146bfb1567bfa9a" ON public.post USING btree (title);
CREATE UNIQUE INDEX "IDX_cd1bddce36edc3e766798eab37" ON public.post USING btree (slug);
CREATE UNIQUE INDEX "PK_be5fda3aac270b134ff9c21cdee" ON public.post USING btree (id);

CREATE TABLE "post_editors_user" (
  "post_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "post_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "post_editors_user"
            ADD FOREIGN KEY (post_id) REFERENCES post(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "post_editors_user"
            ADD PRIMARY KEY (post_id, user_id);  CREATE INDEX "IDX_4abfcfa0b8814c3522eb488636" ON public.post_editors_user USING btree (user_id);
CREATE INDEX "IDX_9fc34bb51d7a8a50a875dbeff2" ON public.post_editors_user USING btree (post_id);
CREATE UNIQUE INDEX "PK_4399b494a9644933edf0e0d7fcb" ON public.post_editors_user USING btree (post_id, user_id);

CREATE TABLE "product_pricing" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "base_price" numeric NOT NULL,
  "creator_share_percentage" numeric NOT NULL DEFAULT '70'::numeric,
  "tax_rate" numeric NOT NULL,
  "availability_rules" jsonb,
  "deleted_at" timestamp without time zone,
  "product_id" uuid
);

ALTER TABLE "product_pricing"
            ADD FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;  ALTER TABLE "product_pricing"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_96a4a861354899893dcf7c8d313" ON public.product_pricing USING btree (id);

CREATE TABLE "product_programs" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "order_index" double precision NOT NULL,
  "is_primary" boolean NOT NULL DEFAULT false,
  "deleted_at" timestamp without time zone,
  "product_id" uuid,
  "program_id" uuid
);

ALTER TABLE "product_programs"
            ADD FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;  ALTER TABLE "product_programs"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;  ALTER TABLE "product_programs"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_4f4bb57866a8128ded92fb01f90" ON public.product_programs USING btree (id);

CREATE TABLE "product_subscription_plans" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "name" character varying(255) NOT NULL,
  "description" text,
  "type" "product_subscription_plans_type_enum" NOT NULL,
  "price" numeric NOT NULL,
  "base_price" numeric NOT NULL,
  "billing_interval" "product_subscription_plans_billing_interval_enum" NOT NULL,
  "billing_interval_count" integer NOT NULL DEFAULT 1,
  "trial_period_days" integer,
  "features" jsonb,
  "availability_rules" jsonb,
  "deleted_at" timestamp without time zone,
  "product_id" uuid
);

ALTER TABLE "product_subscription_plans"
            ADD FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;  ALTER TABLE "product_subscription_plans"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_bf93ad4daf677b4fec9a61633ad" ON public.product_subscription_plans USING btree (id);

CREATE TABLE "products" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "title" character varying(255) NOT NULL,
  "description" text NOT NULL,
  "thumbnail" character varying,
  "type" "products_type_enum" NOT NULL DEFAULT 'program'::products_type_enum,
  "is_bundle" boolean NOT NULL DEFAULT false,
  "bundle_items" jsonb,
  "metadata" jsonb NOT NULL,
  "referral_commission_percentage" numeric NOT NULL DEFAULT '30'::numeric,
  "max_affiliate_discount" numeric NOT NULL DEFAULT '0'::numeric,
  "affiliate_commission_percentage" numeric NOT NULL DEFAULT '30'::numeric,
  "visibility" "products_visibility_enum" NOT NULL DEFAULT 'draft'::products_visibility_enum,
  "deleted_at" timestamp without time zone
);

ALTER TABLE "products"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_5b0f3fe151f941e51d4491cfa8" ON public.products USING btree (is_bundle);
CREATE INDEX "IDX_995d8194c43edfc98838cabc5a" ON public.products USING btree (created_at);
CREATE INDEX "IDX_ce2ba38200f73815993d6e96de" ON public.products USING btree (visibility);
CREATE INDEX "IDX_d5662d5ea5da62fc54b0f12a46" ON public.products USING btree (type);
CREATE UNIQUE INDEX "PK_0806c755e0aca124e67c0cf6d7d" ON public.products USING btree (id);

CREATE TABLE "program_contents" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "type" "program_contents_type_enum" NOT NULL,
  "title" character varying(255) NOT NULL,
  "summary" text,
  "order" double precision NOT NULL,
  "body" jsonb NOT NULL,
  "previewable" boolean NOT NULL DEFAULT false,
  "due_date" timestamp without time zone,
  "available_from" timestamp without time zone,
  "available_to" timestamp without time zone,
  "grading_method" "program_contents_grading_method_enum",
  "duration_minutes" integer,
  "text_response" boolean NOT NULL DEFAULT false,
  "url_response" boolean NOT NULL DEFAULT false,
  "file_response_extensions" jsonb,
  "grading_rubric" jsonb,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "program_id" uuid,
  "parent_id" uuid
);

ALTER TABLE "program_contents"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;  ALTER TABLE "program_contents"
            ADD FOREIGN KEY (parent_id) REFERENCES program_contents(id) ON DELETE CASCADE;  ALTER TABLE "program_contents"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_2a00830247b0e93cb6c9d6a168" ON public.program_contents USING btree (program_id);
CREATE INDEX "IDX_4ae43a5f02434f93cf29cb749d" ON public.program_contents USING btree (parent_id, previewable);
CREATE INDEX "IDX_745ed5daf12f8291044fbc4450" ON public.program_contents USING btree (previewable);
CREATE INDEX "IDX_788346b7a51507192230730db7" ON public.program_contents USING btree (parent_id);
CREATE INDEX "IDX_8eb4657a44b37f7bb0423c6784" ON public.program_contents USING btree (parent_id, "order");
CREATE INDEX "IDX_940a83edd0d1a685b73ac8bb39" ON public.program_contents USING btree (program_id, "order");
CREATE INDEX "IDX_9f6833d8894e69409e75271965" ON public.program_contents USING btree (available_from, available_to);
CREATE INDEX "IDX_c18934543aa5eebcb3d4a34a66" ON public.program_contents USING btree (type);
CREATE INDEX "IDX_e1730f758bb452e2a32d6be228" ON public.program_contents USING btree (due_date);
CREATE UNIQUE INDEX "PK_8652fc3c6cbfc6aef46914e0bd9" ON public.program_contents USING btree (id);

CREATE TABLE "program_feedback_submissions" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "feedback_responses" jsonb NOT NULL,
  "feedback_template_version" character varying NOT NULL,
  "submitted_at" timestamp without time zone NOT NULL DEFAULT now(),
  "ip_address" character varying,
  "user_agent" text,
  "is_valid" boolean NOT NULL DEFAULT true,
  "user_id" uuid,
  "program_id" uuid,
  "product_id" uuid,
  "program_user_id" uuid
);

ALTER TABLE "program_feedback_submissions"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id);  ALTER TABLE "program_feedback_submissions"
            ADD FOREIGN KEY (product_id) REFERENCES products(id);  ALTER TABLE "program_feedback_submissions"
            ADD FOREIGN KEY (program_user_id) REFERENCES program_users(id);  ALTER TABLE "program_feedback_submissions"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON DELETE CASCADE;  ALTER TABLE "program_feedback_submissions"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_a27f818de2c259092282649f215" ON public.program_feedback_submissions USING btree (id);

CREATE TABLE "program_ratings" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "rating" integer NOT NULL,
  "review_title" character varying,
  "review_text" text,
  "content_quality_rating" double precision,
  "instructor_rating" double precision,
  "difficulty_rating" double precision,
  "value_rating" double precision,
  "submitted_at" timestamp without time zone NOT NULL DEFAULT now(),
  "ip_address" character varying,
  "user_agent" text,
  "is_public" boolean NOT NULL DEFAULT true,
  "is_verified" boolean NOT NULL DEFAULT false,
  "moderation_status" "program_ratings_moderation_status_enum" NOT NULL DEFAULT 'pending'::program_ratings_moderation_status_enum,
  "moderation_notes" text,
  "moderated_at" timestamp without time zone,
  "user_id" uuid NOT NULL,
  "program_id" uuid,
  "product_id" uuid,
  "program_user_id" uuid,
  "moderated_by_id" uuid
);

ALTER TABLE "program_ratings"
            ADD FOREIGN KEY (product_id) REFERENCES products(id);  ALTER TABLE "program_ratings"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id);  ALTER TABLE "program_ratings"
            ADD FOREIGN KEY (program_user_id) REFERENCES program_users(id);  ALTER TABLE "program_ratings"
            ADD FOREIGN KEY (moderated_by_id) REFERENCES "user"(id);  ALTER TABLE "program_ratings"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  ALTER TABLE "program_ratings"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_1d9dec735da6a8fa878376fa34" ON public.program_ratings USING btree (moderation_status, submitted_at);
CREATE INDEX "IDX_271f6fab838387902f291e38b9" ON public.program_ratings USING btree (is_public, moderation_status);
CREATE INDEX "IDX_87b9687f34eecf7f845fad5a9b" ON public.program_ratings USING btree (rating, submitted_at);
CREATE INDEX "IDX_ba6419d58689aec1b8b61fa01e" ON public.program_ratings USING btree (program_id, rating);
CREATE INDEX "IDX_e381f739722db5b73ee0dd0c86" ON public.program_ratings USING btree (product_id, rating);
CREATE UNIQUE INDEX "IDX_8a096e1dd761ef5b88c85b6763" ON public.program_ratings USING btree (user_id, program_id) WHERE (program_id IS NOT NULL);
CREATE UNIQUE INDEX "IDX_f4e8bd0c60d52a3e63e5985824" ON public.program_ratings USING btree (user_id, product_id) WHERE (product_id IS NOT NULL);
CREATE UNIQUE INDEX "PK_d653dbffa3cd8b66a337276efda" ON public.program_ratings USING btree (id);

CREATE TABLE "program_user_roles" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "role" "program_user_roles_role_enum" NOT NULL,
  "deleted_at" timestamp without time zone,
  "program_id" uuid,
  "program_user_id" uuid
);

ALTER TABLE "program_user_roles"
            ADD FOREIGN KEY (program_user_id) REFERENCES program_users(id) ON DELETE CASCADE;  ALTER TABLE "program_user_roles"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;  ALTER TABLE "program_user_roles"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_7b4c06031cac4c8dc3598306c4d" ON public.program_user_roles USING btree (id);

CREATE TABLE "program_users" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "analytics" jsonb NOT NULL DEFAULT '{}'::jsonb,
  "grades" jsonb NOT NULL DEFAULT '{}'::jsonb,
  "progress" jsonb NOT NULL DEFAULT '{}'::jsonb,
  "deleted_at" timestamp without time zone,
  "user_product_id" uuid,
  "program_id" uuid
);

ALTER TABLE "program_users"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;  ALTER TABLE "program_users"
            ADD FOREIGN KEY (user_product_id) REFERENCES user_products(id) ON DELETE CASCADE;  ALTER TABLE "program_users"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_1c5d533ae17f9d4db2d0185b80" ON public.program_users USING btree (user_product_id) WHERE (deleted_at IS NULL);
CREATE INDEX "IDX_8386bbf0d442848d7be2993fbb" ON public.program_users USING btree (program_id) WHERE (deleted_at IS NULL);
CREATE UNIQUE INDEX "IDX_7c4e86ab4c02a65ee72992cb57" ON public.program_users USING btree (user_product_id, program_id) WHERE (deleted_at IS NULL);
CREATE UNIQUE INDEX "PK_6cd9ef478a28b846fc1bf51bed7" ON public.program_users USING btree (id);

CREATE TABLE "programs" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "slug" character varying(255) NOT NULL,
  "summary" text NOT NULL,
  "body" jsonb NOT NULL,
  "tenancy_domains" jsonb,
  "cached_enrollment_count" integer NOT NULL DEFAULT 0,
  "cached_completion_count" integer NOT NULL DEFAULT 0,
  "cached_students_completion_rate" numeric NOT NULL DEFAULT '0'::numeric,
  "cached_rating" numeric NOT NULL DEFAULT '0'::numeric,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone
);

ALTER TABLE "programs"
            ADD PRIMARY KEY (id);  ALTER TABLE "programs"
            ADD UNIQUE (slug);  CREATE INDEX "IDX_3381fedfde637cded46e8b0a77" ON public.programs USING btree (cached_rating);
CREATE INDEX "IDX_6782795eacefb9ec300673f6f2" ON public.programs USING btree (cached_enrollment_count);
CREATE INDEX "IDX_cd28bc7f50ea4bb46771d0ec9b" ON public.programs USING btree (created_at);
CREATE INDEX "IDX_e39bc1bb7b6fdddc3ccc0c6d4b" ON public.programs USING btree (tenancy_domains);
CREATE INDEX "IDX_e9fc8a6502df1912d4eaae8132" ON public.programs USING btree (cached_students_completion_rate);
CREATE UNIQUE INDEX "IDX_4180c2bfa0402878a63b70cb4a" ON public.programs USING btree (slug);
CREATE UNIQUE INDEX "PK_d43c664bcaafc0e8a06dfd34e05" ON public.programs USING btree (id);
CREATE UNIQUE INDEX "UQ_4180c2bfa0402878a63b70cb4a4" ON public.programs USING btree (slug);

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
            ADD FOREIGN KEY (banner_id) REFERENCES images(id) ON DELETE CASCADE;  ALTER TABLE "project"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "project"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "project"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_0c2943d87d6b4f22558a7fce71" ON public.project USING btree (visibility);
CREATE INDEX "IDX_cb001317127de4d5e323b5c0c4" ON public.project USING btree (title);
CREATE UNIQUE INDEX "IDX_6fce32ddd71197807027be6ad3" ON public.project USING btree (slug);
CREATE UNIQUE INDEX "PK_4d68b1358bb5b766d3e78f32f57" ON public.project USING btree (id);

CREATE TABLE "project_editors_user" (
  "project_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "project_editors_user"
            ADD FOREIGN KEY (project_id) REFERENCES project(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "project_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "project_editors_user"
            ADD PRIMARY KEY (project_id, user_id);  CREATE INDEX "IDX_4efce1aaecf2b3d2523959a501" ON public.project_editors_user USING btree (project_id);
CREATE INDEX "IDX_8f5fba1aed12561d33ab675a24" ON public.project_editors_user USING btree (user_id);
CREATE UNIQUE INDEX "PK_efd60535d94affeccba7365e591" ON public.project_editors_user USING btree (project_id, user_id);

CREATE TABLE "project_feedback_response" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "responses" jsonb NOT NULL,
  "version_id" uuid,
  "user_id" uuid
);

ALTER TABLE "project_feedback_response"
            ADD FOREIGN KEY (version_id) REFERENCES project_version(id);  ALTER TABLE "project_feedback_response"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  ALTER TABLE "project_feedback_response"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_8ecbac3eb61113faffe1166e11e" ON public.project_feedback_response USING btree (id);

CREATE TABLE "project_screenshots_images" (
  "project_id" uuid NOT NULL,
  "images_id" uuid NOT NULL
);

ALTER TABLE "project_screenshots_images"
            ADD FOREIGN KEY (project_id) REFERENCES project(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "project_screenshots_images"
            ADD FOREIGN KEY (images_id) REFERENCES images(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "project_screenshots_images"
            ADD PRIMARY KEY (project_id, images_id);  CREATE INDEX "IDX_4e1f8d02befe1e5a392910b624" ON public.project_screenshots_images USING btree (project_id);
CREATE INDEX "IDX_ef0ec5b9da603747097dea89a4" ON public.project_screenshots_images USING btree (images_id);
CREATE UNIQUE INDEX "PK_2e301330d17fb5c8f1695a51e39" ON public.project_screenshots_images USING btree (project_id, images_id);

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
            ADD FOREIGN KEY (project_id) REFERENCES project(id);  ALTER TABLE "project_version"
            ADD PRIMARY KEY (id);  ALTER TABLE "project_version"
            ADD UNIQUE (version, project_id);  CREATE INDEX "IDX_4b6747e098d96a0e805692810d" ON public.project_version USING btree (feedback_deadline);
CREATE UNIQUE INDEX "PK_249c24f66d8f7e8ea6f9ff462fb" ON public.project_version USING btree (id);
CREATE UNIQUE INDEX "UQ_b9103221778444dc9638d3775da" ON public.project_version USING btree (version, project_id);

CREATE TABLE "promo_code_uses" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "discount_applied" numeric NOT NULL,
  "used_at" timestamp without time zone NOT NULL DEFAULT now(),
  "promo_code_id" uuid NOT NULL,
  "user_id" uuid NOT NULL,
  "financial_transaction_id" uuid NOT NULL
);

ALTER TABLE "promo_code_uses"
            ADD FOREIGN KEY (financial_transaction_id) REFERENCES financial_transactions(id);  ALTER TABLE "promo_code_uses"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id);  ALTER TABLE "promo_code_uses"
            ADD FOREIGN KEY (promo_code_id) REFERENCES promo_codes(id);  ALTER TABLE "promo_code_uses"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_3510b9afbd630080495fbcbd80" ON public.promo_code_uses USING btree (used_at);
CREATE INDEX "IDX_845967e02e20790222d217b13f" ON public.promo_code_uses USING btree (financial_transaction_id);
CREATE INDEX "IDX_84e9d82f9d6e3c9e53f5d19988" ON public.promo_code_uses USING btree (user_id);
CREATE INDEX "IDX_d82ddb9839c0092afc178357ef" ON public.promo_code_uses USING btree (promo_code_id);
CREATE UNIQUE INDEX "PK_d5e6f1cb226ee85a66aa1b14b2c" ON public.promo_code_uses USING btree (id);

CREATE TABLE "promo_codes" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "code" character varying(50) NOT NULL,
  "type" "promo_codes_type_enum" NOT NULL,
  "discount_percentage" numeric,
  "affiliate_percentage" numeric,
  "max_uses" integer,
  "uses_count" integer NOT NULL DEFAULT 0,
  "max_uses_per_user" integer NOT NULL DEFAULT 1,
  "starts_at" timestamp without time zone NOT NULL DEFAULT now(),
  "expires_at" timestamp without time zone,
  "is_active" boolean NOT NULL DEFAULT true,
  "deleted_at" timestamp without time zone,
  "product_id" uuid,
  "affiliate_user_id" uuid,
  "created_by" uuid
);

ALTER TABLE "promo_codes"
            ADD FOREIGN KEY (affiliate_user_id) REFERENCES "user"(id);  ALTER TABLE "promo_codes"
            ADD FOREIGN KEY (product_id) REFERENCES products(id);  ALTER TABLE "promo_codes"
            ADD FOREIGN KEY (created_by) REFERENCES "user"(id);  ALTER TABLE "promo_codes"
            ADD PRIMARY KEY (id);  ALTER TABLE "promo_codes"
            ADD UNIQUE (code);  CREATE UNIQUE INDEX "PK_c7b4f01710fda5afa056a2b4a35" ON public.promo_codes USING btree (id);
CREATE UNIQUE INDEX "UQ_2f096c406a9d9d5b8ce204190c3" ON public.promo_codes USING btree (code);

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
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "proposal"
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "proposal"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_175c19011e5f5baf732945a311" ON public.proposal USING btree (title);
CREATE INDEX "IDX_f0f2c5e15690f642f3afcde829" ON public.proposal USING btree (visibility);
CREATE UNIQUE INDEX "IDX_894de7603d8172deb20277c6f5" ON public.proposal USING btree (slug);
CREATE UNIQUE INDEX "PK_ca872ecfe4fef5720d2d39e4275" ON public.proposal USING btree (id);

CREATE TABLE "proposal_editors_user" (
  "proposal_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "proposal_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "proposal_editors_user"
            ADD FOREIGN KEY (proposal_id) REFERENCES proposal(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "proposal_editors_user"
            ADD PRIMARY KEY (proposal_id, user_id);  CREATE INDEX "IDX_2469607eeb33fa61f32bda5326" ON public.proposal_editors_user USING btree (user_id);
CREATE INDEX "IDX_d5efb3b9d5fb4cd25e2f1eb849" ON public.proposal_editors_user USING btree (proposal_id);
CREATE UNIQUE INDEX "PK_3fead86d04749b09cebb8fd953d" ON public.proposal_editors_user USING btree (proposal_id, user_id);

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
            ADD FOREIGN KEY (thumbnail_id) REFERENCES images(id);  ALTER TABLE "quiz"
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "quiz"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_37105dd2b728dfbba2f313c907" ON public.quiz USING btree (visibility);
CREATE INDEX "IDX_91b3636bd5cc303c7409c55088" ON public.quiz USING btree (title);
CREATE UNIQUE INDEX "IDX_80e0595aa5572aca26e681ee1e" ON public.quiz USING btree (slug);
CREATE UNIQUE INDEX "PK_422d974e7217414e029b3e641d0" ON public.quiz USING btree (id);

CREATE TABLE "quiz_editors_user" (
  "quiz_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "quiz_editors_user"
            ADD FOREIGN KEY (quiz_id) REFERENCES quiz(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "quiz_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "quiz_editors_user"
            ADD PRIMARY KEY (quiz_id, user_id);  CREATE INDEX "IDX_536f6eac3215f5823c76623df1" ON public.quiz_editors_user USING btree (quiz_id);
CREATE INDEX "IDX_f09c6797b5611ed29a94d7df8b" ON public.quiz_editors_user USING btree (user_id);
CREATE UNIQUE INDEX "PK_fbb93aa1e4225ccb62d11a796c0" ON public.quiz_editors_user USING btree (quiz_id, user_id);

CREATE TABLE "tag_proficiencies" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "proficiency_level" "tag_proficiencies_proficiency_level_enum" NOT NULL,
  "proficiency_level_value" double precision NOT NULL,
  "description" text,
  "prerequisites" jsonb,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "tag_id" uuid
);

ALTER TABLE "tag_proficiencies"
            ADD FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE;  ALTER TABLE "tag_proficiencies"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_13a27f06ef2e3f70d1bd88f1dd" ON public.tag_proficiencies USING btree (proficiency_level);
CREATE INDEX "IDX_345d7b97c4909b5ea559c914ef" ON public.tag_proficiencies USING btree (tag_id);
CREATE INDEX "IDX_744364c4c8c528f26673dd64a6" ON public.tag_proficiencies USING btree (proficiency_level_value);
CREATE INDEX "IDX_b8d1fe1a0510ae5012bd7b1898" ON public.tag_proficiencies USING btree (deleted_at);
CREATE UNIQUE INDEX "IDX_77d3fb222924a32da12afe5ab0" ON public.tag_proficiencies USING btree (tag_id, proficiency_level_value);
CREATE UNIQUE INDEX "IDX_8bae11b40cccfa5f56fcc31892" ON public.tag_proficiencies USING btree (tag_id, proficiency_level);
CREATE UNIQUE INDEX "PK_4e846dacf04db68651a21aa3ecd" ON public.tag_proficiencies USING btree (id);

CREATE TABLE "tag_relationships" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "type" "tag_relationships_type_enum" NOT NULL,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "source_id" uuid,
  "target_id" uuid
);

ALTER TABLE "tag_relationships"
            ADD FOREIGN KEY (source_id) REFERENCES tags(id) ON DELETE CASCADE;  ALTER TABLE "tag_relationships"
            ADD FOREIGN KEY (target_id) REFERENCES tags(id) ON DELETE CASCADE;  ALTER TABLE "tag_relationships"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_006f1c1f0cbca49effaf09799e9" ON public.tag_relationships USING btree (id);

CREATE TABLE "tags" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "name" character varying(255) NOT NULL,
  "description" text,
  "type" "tags_type_enum" NOT NULL,
  "category" character varying(100),
  "metadata" jsonb,
  "deleted_at" timestamp without time zone
);

ALTER TABLE "tags"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_bcffa2d68eb6e69d436f69b3f1" ON public.tags USING btree (type);
CREATE INDEX "IDX_e66a4ffa4213d40bef0da5bc44" ON public.tags USING btree (category);
CREATE UNIQUE INDEX "IDX_1f0d687740481a26473720e159" ON public.tags USING btree (name, type);
CREATE UNIQUE INDEX "PK_e7dc17249a1148a1970748eda99" ON public.tags USING btree (id);

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
            ADD FOREIGN KEY (owner_id) REFERENCES "user"(id);  ALTER TABLE "ticket"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_d9a0835407701eb86f874474b7c" ON public.ticket USING btree (id);

CREATE TABLE "ticket_editors_user" (
  "ticket_id" uuid NOT NULL,
  "user_id" uuid NOT NULL
);

ALTER TABLE "ticket_editors_user"
            ADD FOREIGN KEY (ticket_id) REFERENCES ticket(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "ticket_editors_user"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON UPDATE CASCADE ON DELETE CASCADE;  ALTER TABLE "ticket_editors_user"
            ADD PRIMARY KEY (ticket_id, user_id);  CREATE INDEX "IDX_1d5c90934caa7c88aef8de3798" ON public.ticket_editors_user USING btree (ticket_id);
CREATE INDEX "IDX_68a614c8fd4410c06e03e0fa84" ON public.ticket_editors_user USING btree (user_id);
CREATE UNIQUE INDEX "PK_cbe7f143227c03a658062234a49" ON public.ticket_editors_user USING btree (ticket_id, user_id);

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

CREATE UNIQUE INDEX "IDX_189473aaba06ffd667bb024e71" ON public."user" USING btree (facebook_id);
CREATE UNIQUE INDEX "IDX_45bb0502759f0dd73c4fd8b13b" ON public."user" USING btree (github_id);
CREATE UNIQUE INDEX "IDX_55008adb3b4101af12f495c9c1" ON public."user" USING btree (twitter_id);
CREATE UNIQUE INDEX "IDX_78a916df40e02a9deb1c4b75ed" ON public."user" USING btree (username);
CREATE UNIQUE INDEX "IDX_7adac5c0b28492eb292d4a9387" ON public."user" USING btree (google_id);
CREATE UNIQUE INDEX "IDX_ac2af862c8540eccb210b29310" ON public."user" USING btree (wallet_address);
CREATE UNIQUE INDEX "IDX_e12875dfb3b1d92d7d7c5377e2" ON public."user" USING btree (email);
CREATE UNIQUE INDEX "IDX_f0f25ac3307013265a678db85e" ON public."user" USING btree (linkedin_id);
CREATE UNIQUE INDEX "IDX_fda2d885fb612212b85752f5ab" ON public."user" USING btree (apple_id);
CREATE UNIQUE INDEX "PK_cace4a159ff9f2512dd42373760" ON public."user" USING btree (id);
CREATE UNIQUE INDEX "UQ_189473aaba06ffd667bb024e71a" ON public."user" USING btree (facebook_id);
CREATE UNIQUE INDEX "UQ_45bb0502759f0dd73c4fd8b13bd" ON public."user" USING btree (github_id);
CREATE UNIQUE INDEX "UQ_55008adb3b4101af12f495c9c1d" ON public."user" USING btree (twitter_id);
CREATE UNIQUE INDEX "UQ_78a916df40e02a9deb1c4b75edb" ON public."user" USING btree (username);
CREATE UNIQUE INDEX "UQ_7adac5c0b28492eb292d4a93871" ON public."user" USING btree (google_id);
CREATE UNIQUE INDEX "UQ_ac2af862c8540eccb210b293107" ON public."user" USING btree (wallet_address);
CREATE UNIQUE INDEX "UQ_e12875dfb3b1d92d7d7c5377e22" ON public."user" USING btree (email);
CREATE UNIQUE INDEX "UQ_f0f25ac3307013265a678db85e4" ON public."user" USING btree (linkedin_id);
CREATE UNIQUE INDEX "UQ_f44d0cd18cfd80b0fed7806c3b7" ON public."user" USING btree (profile_id);
CREATE UNIQUE INDEX "UQ_fda2d885fb612212b85752f5ab1" ON public."user" USING btree (apple_id);

CREATE TABLE "user_certificates" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "certificate_number" character varying(100) NOT NULL,
  "status" "user_certificates_status_enum" NOT NULL,
  "issued_at" timestamp without time zone NOT NULL,
  "expires_at" timestamp without time zone,
  "revoked_at" timestamp without time zone,
  "revocation_reason" text,
  "certificate_url" character varying,
  "metadata" jsonb,
  "deleted_at" timestamp without time zone,
  "program_id" uuid,
  "product_id" uuid,
  "user_id" uuid,
  "program_user_id" uuid,
  "certificate_id" uuid
);

ALTER TABLE "user_certificates"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON DELETE CASCADE;  ALTER TABLE "user_certificates"
            ADD FOREIGN KEY (program_id) REFERENCES programs(id) ON DELETE CASCADE;  ALTER TABLE "user_certificates"
            ADD FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;  ALTER TABLE "user_certificates"
            ADD FOREIGN KEY (certificate_id) REFERENCES certificates(id) ON DELETE CASCADE;  ALTER TABLE "user_certificates"
            ADD FOREIGN KEY (program_user_id) REFERENCES program_users(id) ON DELETE CASCADE;  ALTER TABLE "user_certificates"
            ADD PRIMARY KEY (id);  ALTER TABLE "user_certificates"
            ADD UNIQUE (certificate_number);  CREATE INDEX "IDX_0fb66c2b2d4edbb223682730e8" ON public.user_certificates USING btree (user_id);
CREATE INDEX "IDX_122c8480175f9e5c6166768a13" ON public.user_certificates USING btree (program_id);
CREATE INDEX "IDX_1eb0cddc4690c9153e3bf5cf0b" ON public.user_certificates USING btree (product_id);
CREATE INDEX "IDX_5d5e924c347ffb90821fce8534" ON public.user_certificates USING btree (certificate_id);
CREATE INDEX "IDX_8e6d34aa76b22cd2cb5dbfed53" ON public.user_certificates USING btree (issued_at);
CREATE INDEX "IDX_a243781516ea785025af146b6e" ON public.user_certificates USING btree (expires_at);
CREATE INDEX "IDX_f8ae29f5803a17157f033ff96a" ON public.user_certificates USING btree (program_user_id);
CREATE INDEX "IDX_f91b60ef6aeb3549d79a3081e6" ON public.user_certificates USING btree (status);
CREATE UNIQUE INDEX "IDX_62166957dad7b82241c3888c2e" ON public.user_certificates USING btree (certificate_number);
CREATE UNIQUE INDEX "PK_58e6b21aa02f666a20886d895ac" ON public.user_certificates USING btree (id);
CREATE UNIQUE INDEX "UQ_62166957dad7b82241c3888c2e0" ON public.user_certificates USING btree (certificate_number);

CREATE TABLE "user_financial_methods" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "user_id" uuid NOT NULL,
  "method_type" "user_financial_methods_method_type_enum" NOT NULL,
  "provider_method_id" character varying(255),
  "last_four_digits" character varying(100),
  "brand" character varying(100),
  "expiry_month" integer,
  "expiry_year" integer,
  "is_active" boolean NOT NULL DEFAULT true,
  "is_default" boolean NOT NULL DEFAULT false,
  "status" "user_financial_methods_status_enum" NOT NULL DEFAULT 'active'::user_financial_methods_status_enum,
  "metadata" jsonb
);

ALTER TABLE "user_financial_methods"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON DELETE CASCADE;  ALTER TABLE "user_financial_methods"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_bcc4dfbc00db2f49919b3399fd" ON public.user_financial_methods USING btree (user_id, status);
CREATE UNIQUE INDEX "PK_b0118602348fc1d679602903966" ON public.user_financial_methods USING btree (id);

CREATE TABLE "user_kyc_verifications" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "provider" "user_kyc_verifications_provider_enum" NOT NULL,
  "provider_applicant_id" character varying NOT NULL,
  "provider_verification_id" character varying NOT NULL,
  "verification_level" character varying NOT NULL,
  "status" "user_kyc_verifications_status_enum" NOT NULL DEFAULT 'pending'::user_kyc_verifications_status_enum,
  "last_check_at" timestamp without time zone NOT NULL,
  "approved_at" timestamp without time zone,
  "rejected_at" timestamp without time zone,
  "rejection_reason" text,
  "expires_at" timestamp without time zone,
  "metadata" jsonb,
  "user_id" uuid
);

ALTER TABLE "user_kyc_verifications"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON DELETE CASCADE;  ALTER TABLE "user_kyc_verifications"
            ADD PRIMARY KEY (id);  ALTER TABLE "user_kyc_verifications"
            ADD UNIQUE (provider_applicant_id);  ALTER TABLE "user_kyc_verifications"
            ADD UNIQUE (provider_verification_id);  CREATE UNIQUE INDEX "PK_b2713fe3f62fa4a2731c72dac33" ON public.user_kyc_verifications USING btree (id);
CREATE UNIQUE INDEX "UQ_3cbbc217c93d23bf19132eb0fcc" ON public.user_kyc_verifications USING btree (provider_applicant_id);
CREATE UNIQUE INDEX "UQ_d8ffff8cfd13fe59f7eb8f17f0e" ON public.user_kyc_verifications USING btree (provider_verification_id);

CREATE TABLE "user_products" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "acquisition_type" "user_products_acquisition_type_enum" NOT NULL,
  "access_expires_at" timestamp without time zone,
  "access_started_at" timestamp without time zone NOT NULL DEFAULT now(),
  "status" "user_products_status_enum" NOT NULL DEFAULT 'active'::user_products_status_enum,
  "metadata" jsonb NOT NULL,
  "deleted_at" timestamp without time zone,
  "user_id" uuid,
  "product_id" uuid,
  "subscription_id" uuid,
  "purchase_transaction_id" uuid
);

ALTER TABLE "user_products"
            ADD FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE;  ALTER TABLE "user_products"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON DELETE CASCADE;  ALTER TABLE "user_products"
            ADD FOREIGN KEY (purchase_transaction_id) REFERENCES financial_transactions(id);  ALTER TABLE "user_products"
            ADD FOREIGN KEY (subscription_id) REFERENCES user_subscriptions(id);  ALTER TABLE "user_products"
            ADD PRIMARY KEY (id);  CREATE INDEX "IDX_2d4f33717491f83635416f67b3" ON public.user_products USING btree (access_expires_at);
CREATE INDEX "IDX_8524bda5fc20e6b2e064391198" ON public.user_products USING btree (acquisition_type);
CREATE INDEX "IDX_8b059a1db24054ba759c7de408" ON public.user_products USING btree (status);
CREATE INDEX "IDX_b9470e455b81e2f0bc0d32f269" ON public.user_products USING btree (user_id, product_id);
CREATE INDEX "IDX_f87fb160bbd9d4d52bd67624a1" ON public.user_products USING btree (subscription_id);
CREATE UNIQUE INDEX "PK_347cc741febfe07d6d46d048fb4" ON public.user_products USING btree (id);

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
            ADD FOREIGN KEY (picture_id) REFERENCES images(id);  ALTER TABLE "user_profile"
            ADD PRIMARY KEY (id);  ALTER TABLE "user_profile"
            ADD UNIQUE (picture_id);  CREATE UNIQUE INDEX "PK_f44d0cd18cfd80b0fed7806c3b7" ON public.user_profile USING btree (id);
CREATE UNIQUE INDEX "UQ_e4238c3828bc51ff8ca27c46385" ON public.user_profile USING btree (picture_id);

CREATE TABLE "user_subscriptions" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now(),
  "status" "user_subscriptions_status_enum" NOT NULL,
  "current_period_start" timestamp without time zone NOT NULL,
  "current_period_end" timestamp without time zone NOT NULL,
  "cancel_at_period_end" boolean NOT NULL DEFAULT false,
  "canceled_at" timestamp without time zone,
  "ended_at" timestamp without time zone,
  "trial_end" timestamp without time zone,
  "deleted_at" timestamp without time zone,
  "user_id" uuid,
  "subscription_plan_id" uuid
);

ALTER TABLE "user_subscriptions"
            ADD FOREIGN KEY (user_id) REFERENCES "user"(id) ON DELETE CASCADE;  ALTER TABLE "user_subscriptions"
            ADD FOREIGN KEY (subscription_plan_id) REFERENCES product_subscription_plans(id);  ALTER TABLE "user_subscriptions"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_9e928b0954e51705ab44988812c" ON public.user_subscriptions USING btree (id);

CREATE TABLE "vote" (
  "id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "created_at" timestamp without time zone NOT NULL DEFAULT now(),
  "updated_at" timestamp without time zone NOT NULL DEFAULT now()
);

ALTER TABLE "vote"
            ADD PRIMARY KEY (id);  CREATE UNIQUE INDEX "PK_2d5932d46afe39c8176f9d4be72" ON public.vote USING btree (id);

