-- Additive migration: keep user IDs, password hashes, legacy roles and outbox unchanged.
ALTER TABLE auth_users ALTER COLUMN password_hash DROP NOT NULL;
CREATE TABLE auth_external_accounts (
 provider varchar(10) NOT NULL CHECK (provider IN ('google','github')),
 subject varchar(255) NOT NULL,
 user_id uuid NOT NULL REFERENCES auth_users(id) ON DELETE RESTRICT,
 verified_email varchar(254) NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(provider,subject), UNIQUE(user_id,provider)
);
CREATE TABLE auth_oauth_attempts (
 state_hash char(64) PRIMARY KEY,
 browser_hash char(64) NOT NULL,
 provider varchar(10) NOT NULL CHECK (provider IN ('google','github')),
 verifier varchar(128) NOT NULL,
 nonce varchar(128) NOT NULL,
 expires_at timestamptz NOT NULL
);
CREATE INDEX auth_oauth_attempt_expiry ON auth_oauth_attempts(expires_at);
CREATE TABLE auth_oauth_tickets (
 ticket_hash char(64) PRIMARY KEY,
 browser_hash char(64) NOT NULL,
 user_id uuid NOT NULL REFERENCES auth_users(id) ON DELETE CASCADE,
 provider varchar(10) NOT NULL CHECK (provider IN ('google','github')),
 expires_at timestamptz NOT NULL
);
CREATE INDEX auth_oauth_ticket_expiry ON auth_oauth_tickets(expires_at);
