import type {
  MigrationInterface,
  QueryRunner,
} from 'typeorm';

export class AddOAuthExternalAccounts1793750400000
  implements MigrationInterface
{
  name = 'AddOAuthExternalAccounts1793750400000';

  public async up(
    queryRunner: QueryRunner,
  ): Promise<void> {
    await queryRunner.query(`
      ALTER TABLE "users"
      ALTER COLUMN "password_hash" DROP NOT NULL
    `);

    await queryRunner.query(`
      UPDATE "roles"
      SET "name" = 'ADMIN',
          "description" = 'Administrador del sistema'
      WHERE "name" = 'Administrator'
    `);

    await queryRunner.query(`
      UPDATE "roles"
      SET "name" = 'OPERATOR',
          "description" = 'Operador del sistema'
      WHERE "name" = 'Operator'
    `);

    await queryRunner.query(`
      UPDATE "roles"
      SET "name" = 'VIEWER',
          "description" = 'Usuario de consulta'
      WHERE "name" = 'ConsultationUser'
    `);

    await queryRunner.query(`
      CREATE TABLE "external_accounts" (
        "id" uuid NOT NULL DEFAULT gen_random_uuid(),
        "user_id" uuid NOT NULL,
        "provider" character varying(50) NOT NULL,
        "provider_user_id" character varying(255) NOT NULL,
        "provider_email" character varying(254),
        "created_at" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),

        CONSTRAINT "PK_external_accounts"
          PRIMARY KEY ("id"),

        CONSTRAINT "UQ_external_provider_user"
          UNIQUE ("provider", "provider_user_id"),

        CONSTRAINT "UQ_external_user_provider"
          UNIQUE ("user_id", "provider"),

        CONSTRAINT "FK_external_accounts_user"
          FOREIGN KEY ("user_id")
          REFERENCES "users"("id")
          ON DELETE CASCADE
          ON UPDATE CASCADE
      )
    `);

    await queryRunner.query(`
      CREATE INDEX "IDX_external_accounts_user"
      ON "external_accounts" ("user_id")
    `);

    await queryRunner.query(`
      CREATE INDEX "IDX_external_accounts_provider"
      ON "external_accounts" ("provider")
    `);
  }

  public async down(
    queryRunner: QueryRunner,
  ): Promise<void> {
    await queryRunner.query(`
      DROP INDEX IF EXISTS "IDX_external_accounts_provider"
    `);

    await queryRunner.query(`
      DROP INDEX IF EXISTS "IDX_external_accounts_user"
    `);

    await queryRunner.query(`
      DROP TABLE IF EXISTS "external_accounts"
    `);

    await queryRunner.query(`
      UPDATE "roles"
      SET "name" = 'Administrator'
      WHERE "name" = 'ADMIN'
    `);

    await queryRunner.query(`
      UPDATE "roles"
      SET "name" = 'Operator'
      WHERE "name" = 'OPERATOR'
    `);

    await queryRunner.query(`
      UPDATE "roles"
      SET "name" = 'ConsultationUser'
      WHERE "name" = 'VIEWER'
    `);
  }
}
