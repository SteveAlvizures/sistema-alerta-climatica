CREATE TABLE IF NOT EXISTS auth_users (
 id uuid PRIMARY KEY,
 name varchar(150) NOT NULL,
 username varchar(254) NOT NULL,
 password_hash varchar(500) NOT NULL,
 role varchar(80) NOT NULL CHECK (role IN ('Administrator','Operator','ConsultationUser','User')),
 is_active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL,
 last_access_at timestamptz,
 imported_source jsonb
);
CREATE UNIQUE INDEX IF NOT EXISTS auth_users_username_unique ON auth_users(lower(username));
CREATE TABLE IF NOT EXISTS auth_outbox (
 id uuid PRIMARY KEY,
 payload jsonb NOT NULL,
 occurred_at timestamptz NOT NULL DEFAULT now(),
 delivered_at timestamptz,
 attempts integer NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS auth_outbox_pending ON auth_outbox(occurred_at) WHERE delivered_at IS NULL;
