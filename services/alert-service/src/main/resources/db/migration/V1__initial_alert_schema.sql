CREATE TABLE alert_rules (
    id UUID PRIMARY KEY,
    community_id UUID NOT NULL,
    sensor_id UUID NULL,
    code VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    phenomenon VARCHAR(50) NOT NULL,
    variable VARCHAR(50) NOT NULL,
    danger_level VARCHAR(20) NOT NULL,
    lower_limit NUMERIC(18,6),
    upper_limit NUMERIC(18,6),
    valid_from TIMESTAMPTZ NOT NULL,
    valid_until TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL,
    comparison_operator VARCHAR(5) NOT NULL,
    activation_point NUMERIC(18,6) NOT NULL,
    uses_range BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX ix_alert_rules_community
ON alert_rules(community_id);

CREATE INDEX ix_alert_rules_sensor
ON alert_rules(sensor_id);


CREATE TABLE climate_events (
    id UUID PRIMARY KEY,
    community_id UUID NOT NULL,
    phenomenon VARCHAR(50) NOT NULL,
    level VARCHAR(20) NOT NULL,
    status VARCHAR(20) NOT NULL,
    description VARCHAR(1000) NOT NULL,
    detected_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    closed_at TIMESTAMPTZ
);

CREATE INDEX ix_events_community
ON climate_events(community_id);

CREATE INDEX ix_events_status
ON climate_events(status);


CREATE TABLE alerts (
    id UUID PRIMARY KEY,
    community_id UUID NOT NULL,
    rule_id UUID NOT NULL,
    supporting_reading_id UUID NOT NULL UNIQUE,
    sensor_id UUID NOT NULL,
    event_id UUID,
    level VARCHAR(20) NOT NULL,
    phenomenon VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL,
    variable VARCHAR(50) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    detected_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    closed_at TIMESTAMPTZ,
    acknowledged_at TIMESTAMPTZ,
    acknowledged_by_id UUID,
    closed_by_id UUID,
    detected_value NUMERIC(18,6) NOT NULL,
    activation_point_snapshot NUMERIC(18,6) NOT NULL,
    lower_limit_snapshot NUMERIC(18,6),
    upper_limit_snapshot NUMERIC(18,6),
    uses_range_snapshot BOOLEAN,
    comparison_operator_snapshot VARCHAR(5),
    created_at TIMESTAMPTZ NOT NULL,

    CONSTRAINT fk_alert_rule
        FOREIGN KEY(rule_id)
        REFERENCES alert_rules(id),

    CONSTRAINT fk_alert_event
        FOREIGN KEY(event_id)
        REFERENCES climate_events(id)
);

CREATE INDEX ix_alerts_community
ON alerts(community_id);

CREATE INDEX ix_alerts_sensor
ON alerts(sensor_id);

CREATE INDEX ix_alerts_status
ON alerts(status);

CREATE INDEX ix_alerts_detected
ON alerts(detected_at);
