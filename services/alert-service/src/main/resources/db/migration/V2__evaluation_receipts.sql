-- Durable deduplication survives alert transitions and lost acknowledgements.
CREATE TABLE evaluation_receipts (reading_id uuid PRIMARY KEY);
