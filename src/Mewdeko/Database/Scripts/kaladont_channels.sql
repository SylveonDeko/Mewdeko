-- Kaladont channel configuration table
CREATE TABLE IF NOT EXISTS kaladont_channels
(
    id              SERIAL PRIMARY KEY,
    guild_id        BIGINT       NOT NULL,
    channel_id      BIGINT       NOT NULL UNIQUE,
    language        VARCHAR(10)  NOT NULL DEFAULT 'en',
    mode            INT          NOT NULL DEFAULT 0, -- 0 = Normal, 1 = Endless
    turn_time       INT          NOT NULL DEFAULT 30,
    current_word    VARCHAR(255) NOT NULL,
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMP    NOT NULL DEFAULT NOW(),
    total_words     BIGINT       NOT NULL DEFAULT 0,
    current_players TEXT,                            -- JSON array of player IDs currently in game
    INDEX           idx_kaladont_channel(channel_id),
    INDEX           idx_kaladont_guild(guild_id)
);

-- Kaladont game statistics per user per channel
CREATE TABLE IF NOT EXISTS kaladont_stats
(
    id           SERIAL PRIMARY KEY,
    channel_id   BIGINT    NOT NULL,
    user_id      BIGINT    NOT NULL,
    words_count  BIGINT    NOT NULL DEFAULT 0,
    wins         INT       NOT NULL DEFAULT 0,
    eliminations INT       NOT NULL DEFAULT 0,
    last_played  TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (channel_id, user_id),
    INDEX        idx_kaladont_stats_channel(channel_id),
    INDEX        idx_kaladont_stats_user(user_id)
);

-- Kaladont game history
CREATE TABLE IF NOT EXISTS kaladont_games
(
    id            SERIAL PRIMARY KEY,
    channel_id    BIGINT      NOT NULL,
    started_at    TIMESTAMP   NOT NULL DEFAULT NOW(),
    ended_at      TIMESTAMP,
    winner_id     BIGINT,
    total_words   INT         NOT NULL DEFAULT 0,
    total_players INT         NOT NULL DEFAULT 0,
    mode          INT         NOT NULL DEFAULT 0,
    language      VARCHAR(10) NOT NULL,
    INDEX         idx_kaladont_games_channel(channel_id)
);
