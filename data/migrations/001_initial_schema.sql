-- ============================================================
-- Migration: 001_initial_schema.sql
-- Creates all tables for the Tarteel Clone application
-- ============================================================

-- ── Quran Data ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS verses (
    id           SERIAL PRIMARY KEY,
    surah_num    SMALLINT    NOT NULL,
    ayah_num     SMALLINT    NOT NULL,
    arabic_text  TEXT        NOT NULL,
    uthmani_text TEXT,
    UNIQUE (surah_num, ayah_num)
);

CREATE INDEX idx_verses_surah ON verses (surah_num);

CREATE TABLE IF NOT EXISTS translations (
    id         SERIAL PRIMARY KEY,
    verse_id   INT          NOT NULL REFERENCES verses(id) ON DELETE CASCADE,
    language   VARCHAR(10)  NOT NULL,
    text       TEXT         NOT NULL,
    translator VARCHAR(200)
);

CREATE INDEX idx_translations_verse ON translations (verse_id);
CREATE INDEX idx_translations_lang  ON translations (language);

CREATE TABLE IF NOT EXISTS tafsir (
    id       SERIAL PRIMARY KEY,
    verse_id INT          NOT NULL REFERENCES verses(id) ON DELETE CASCADE,
    source   VARCHAR(200),
    content  TEXT         NOT NULL
);

CREATE INDEX idx_tafsir_verse ON tafsir (verse_id);

-- ── User Data ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS users (
    id            SERIAL PRIMARY KEY,
    email         VARCHAR(256) NOT NULL UNIQUE,
    password_hash TEXT         NOT NULL,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS recitation_sessions (
    id         SERIAL PRIMARY KEY,
    user_id    INT         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at   TIMESTAMPTZ
);

CREATE INDEX idx_sessions_user ON recitation_sessions (user_id);

CREATE TABLE IF NOT EXISTS recitation_errors (
    id         SERIAL PRIMARY KEY,
    session_id INT          NOT NULL REFERENCES recitation_sessions(id) ON DELETE CASCADE,
    verse_id   INT          NOT NULL REFERENCES verses(id),
    error_type VARCHAR(100),
    timestamp  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_errors_session ON recitation_errors (session_id);

CREATE TABLE IF NOT EXISTS memorization_progress (
    id            SERIAL PRIMARY KEY,
    user_id       INT     NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    surah_num     SMALLINT NOT NULL,
    ayah_num      SMALLINT NOT NULL,
    mastery_score DOUBLE PRECISION NOT NULL DEFAULT 0.0,
    UNIQUE (user_id, surah_num, ayah_num)
);

CREATE INDEX idx_progress_user ON memorization_progress (user_id);
