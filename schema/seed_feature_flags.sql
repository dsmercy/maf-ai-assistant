-- Seed default feature flags
-- Run: docker exec -i postgres psql -U assistant -d assistant_db < schema/seed_feature_flags.sql

INSERT INTO "FeatureFlags" ("Id", "Name", "IsEnabled", "Description", "CreatedAt", "UpdatedAt")
VALUES
  (gen_random_uuid(), 'streaming',  true,  'Enable token streaming on /api/chat/stream and /v1/chat/completions', NOW(), NOW()),
  (gen_random_uuid(), 'rag',        true,  'Enable RAG retrieval via RepositoryAgent', NOW(), NOW()),
  (gen_random_uuid(), 'auth',       false, 'Enforce JWT authentication on all non-health endpoints', NOW(), NOW()),
  (gen_random_uuid(), 'audit',      true,  'Write all API requests to the audit_logs table', NOW(), NOW()),
  (gen_random_uuid(), 'rate-limit', true,  'Apply rate limiting on POST /api/chat and ingestion endpoints', NOW(), NOW())
ON CONFLICT DO NOTHING;

SELECT "Name", "IsEnabled", "Description" FROM "FeatureFlags" ORDER BY "Name";
