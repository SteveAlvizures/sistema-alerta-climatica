-- Preserve the source external-account UUID during the non-destructive production port.
ALTER TABLE auth_external_accounts ADD COLUMN legacy_id uuid;
CREATE UNIQUE INDEX auth_external_accounts_legacy_id ON auth_external_accounts(legacy_id) WHERE legacy_id IS NOT NULL;
