import { MigrationInterface, QueryRunner } from 'typeorm';

export class RenameDbs1740411578126 implements MigrationInterface {
  name = 'RenameDbs1740411578126';

  public async up(queryRunner: QueryRunner): Promise<void> {
    // rename job-post_editors_user table to job_post_editors_user
    await queryRunner.query(
      `ALTER TABLE "job-post_editors_user"
          RENAME TO "job_post_editors_user"`,
    );

    // rename job-tag to job_tags
    await queryRunner.query(
      `ALTER TABLE "job-tag"
          RENAME TO "job_tag"`,
    );

    // rename job-post to job_posts
    await queryRunner.query(
      `ALTER TABLE "job-post"
          RENAME TO "job_post"`,
    );

    // rename job-post_job_tags_job-tag to job_post_job_tags_job_tags
    await queryRunner.query(
      `ALTER TABLE "job-post_job_tags_job-tag"
          RENAME TO "job_post_job_tags_job_tag"`,
    );

    // rename job-application to job_applications
    await queryRunner.query(
      `ALTER TABLE "job-application"
          RENAME TO "job_application"`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    // rename job_post_editors_user table to job-post_editors_user
    await queryRunner.query(
      `ALTER TABLE "job_post_editors_user"
          RENAME TO "job-post_editors_user"`,
    );

    // rename job_tags to job-tag
    await queryRunner.query(
      `ALTER TABLE "job_tag"
          RENAME TO "job-tag"`,
    );

    // rename job_posts to job-post
    await queryRunner.query(
      `ALTER TABLE "job_posts"
          RENAME TO "job-posts"`,
    );
  }
}
