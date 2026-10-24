CREATE TABLE audit_outbox (
 id uuid PRIMARY KEY, payload jsonb NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 delivered_at timestamptz, attempts integer NOT NULL DEFAULT 0
);
CREATE INDEX ix_audit_outbox_pending ON audit_outbox(created_at) WHERE delivered_at IS NULL;
