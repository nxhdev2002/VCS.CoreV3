-- SQL migration for api_keys table (PostgreSQL)
CREATE TABLE IF NOT EXISTS api_keys (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key_hash VARCHAR(256) NOT NULL,
    user_id UUID NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    expired_at TIMESTAMP WITH TIME ZONE,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    plan VARCHAR(100) NOT NULL DEFAULT 'free',
    rate_limit INT NOT NULL DEFAULT 1000,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_api_keys_key_hash UNIQUE (key_hash),
    INDEX idx_api_keys_lookup (key_hash, expired_at, is_revoked)
);
-- Optional: add foreign key to users table if exists
-- ALTER TABLE api_keys ADD CONSTRAINT fk_api_keys_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;