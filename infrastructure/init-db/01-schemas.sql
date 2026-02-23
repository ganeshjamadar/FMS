-- FundManager: Schema initialization
-- Creates one schema per microservice (schema-per-service isolation)
-- Per research-database.md Section 2 and Constitution Principle I

CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS fundadmin;
CREATE SCHEMA IF NOT EXISTS contributions;
CREATE SCHEMA IF NOT EXISTS loans;
CREATE SCHEMA IF NOT EXISTS dissolution;
CREATE SCHEMA IF NOT EXISTS notifications;
CREATE SCHEMA IF NOT EXISTS audit;

-- Grant usage to the fundmanager role
GRANT USAGE ON SCHEMA identity TO fundmanager;
GRANT USAGE ON SCHEMA fundadmin TO fundmanager;
GRANT USAGE ON SCHEMA contributions TO fundmanager;
GRANT USAGE ON SCHEMA loans TO fundmanager;
GRANT USAGE ON SCHEMA dissolution TO fundmanager;
GRANT USAGE ON SCHEMA notifications TO fundmanager;
GRANT USAGE ON SCHEMA audit TO fundmanager;

-- Grant all privileges on tables that will be created
ALTER DEFAULT PRIVILEGES IN SCHEMA identity GRANT ALL ON TABLES TO fundmanager;
ALTER DEFAULT PRIVILEGES IN SCHEMA fundadmin GRANT ALL ON TABLES TO fundmanager;
ALTER DEFAULT PRIVILEGES IN SCHEMA contributions GRANT ALL ON TABLES TO fundmanager;
ALTER DEFAULT PRIVILEGES IN SCHEMA loans GRANT ALL ON TABLES TO fundmanager;
ALTER DEFAULT PRIVILEGES IN SCHEMA dissolution GRANT ALL ON TABLES TO fundmanager;
ALTER DEFAULT PRIVILEGES IN SCHEMA notifications GRANT ALL ON TABLES TO fundmanager;
ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT ALL ON TABLES TO fundmanager;

-- Enable UUID generation extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
