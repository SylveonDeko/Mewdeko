-- Anti-Mass-Post Protection
CREATE TABLE IF NOT EXISTS antimasspostsettings
(
    id
    SERIAL
    PRIMARY
    KEY,
    guildid
    BIGINT
    NOT
    NULL
    UNIQUE,
    action
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    channelthreshold
    INTEGER
    NOT
    NULL
    DEFAULT
    3,
    timewindowseconds
    INTEGER
    NOT
    NULL
    DEFAULT
    60,
    contentsimilaritythreshold
    DOUBLE
    PRECISION
    NOT
    NULL
    DEFAULT
    0.8,
    mincontentlength
    INTEGER
    NOT
    NULL
    DEFAULT
    20,
    checklinksonly
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    checkduplicatecontent
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    requireidenticalcontent
    BOOLEAN
    NOT
    NULL
    DEFAULT
    FALSE,
    casesensitive
    BOOLEAN
    NOT
    NULL
    DEFAULT
    FALSE,
    deletemessages
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    notifyuser
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    punishduration
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    roleid
    BIGINT
    DEFAULT
    NULL,
    ignorebots
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    maxmessagestracked
    INTEGER
    NOT
    NULL
    DEFAULT
    50,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
)
    );

CREATE TABLE IF NOT EXISTS antimasspostignoredroles
(
    id
    SERIAL
    PRIMARY
    KEY,
    antimasspostsettingid
    INTEGER
    NOT
    NULL,
    roleid
    BIGINT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antimasspostsettingid
) REFERENCES antimasspostsettings
(
    id
) ON DELETE CASCADE
    );

CREATE TABLE IF NOT EXISTS antimasspostignoredusers
(
    id
    SERIAL
    PRIMARY
    KEY,
    antimasspostsettingid
    INTEGER
    NOT
    NULL,
    userid
    BIGINT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antimasspostsettingid
) REFERENCES antimasspostsettings
(
    id
) ON DELETE CASCADE
    );

CREATE TABLE IF NOT EXISTS antimasspostignoredchannels
(
    id
    SERIAL
    PRIMARY
    KEY,
    antimasspostsettingid
    INTEGER
    NOT
    NULL,
    channelid
    BIGINT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antimasspostsettingid
) REFERENCES antimasspostsettings
(
    id
) ON DELETE CASCADE
    );

CREATE TABLE IF NOT EXISTS antimasspostlinkwhitelist
(
    id
    SERIAL
    PRIMARY
    KEY,
    antimasspostsettingid
    INTEGER
    NOT
    NULL,
    domain
    TEXT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antimasspostsettingid
) REFERENCES antimasspostsettings
(
    id
) ON DELETE CASCADE
    );

CREATE TABLE IF NOT EXISTS antimasspostlinkblacklist
(
    id
    SERIAL
    PRIMARY
    KEY,
    antimasspostsettingid
    INTEGER
    NOT
    NULL,
    domain
    TEXT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antimasspostsettingid
) REFERENCES antimasspostsettings
(
    id
) ON DELETE CASCADE
    );

-- Anti-Post-Channel Protection (Honeypot Channels)
CREATE TABLE IF NOT EXISTS antipostchannelsettings
(
    id
    SERIAL
    PRIMARY
    KEY,
    guildid
    BIGINT
    NOT
    NULL
    UNIQUE,
    action
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    deletemessages
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    notifyuser
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    punishduration
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    roleid
    BIGINT
    DEFAULT
    NULL,
    ignorebots
    BOOLEAN
    NOT
    NULL
    DEFAULT
    TRUE,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
)
    );

CREATE TABLE IF NOT EXISTS antipostchannelchannels
(
    id
    SERIAL
    PRIMARY
    KEY,
    antipostchannelsettingid
    INTEGER
    NOT
    NULL,
    channelid
    BIGINT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antipostchannelsettingid
) REFERENCES antipostchannelsettings
(
    id
) ON DELETE CASCADE
    );

CREATE TABLE IF NOT EXISTS antipostchannelignoredroles
(
    id
    SERIAL
    PRIMARY
    KEY,
    antipostchannelsettingid
    INTEGER
    NOT
    NULL,
    roleid
    BIGINT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antipostchannelsettingid
) REFERENCES antipostchannelsettings
(
    id
) ON DELETE CASCADE
    );

CREATE TABLE IF NOT EXISTS antipostchannel_ignoredusers
(
    id
    SERIAL
    PRIMARY
    KEY,
    antipostchannelsettingid
    INTEGER
    NOT
    NULL,
    userid
    BIGINT
    NOT
    NULL,
    dateadded
    TIMESTAMP
    NOT
    NULL
    DEFAULT
    NOW
(
),
    FOREIGN KEY
(
    antipostchannelsettingid
) REFERENCES antipostchannelsettings
(
    id
) ON DELETE CASCADE
    );

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_antimasspostsettings_guildid ON antimasspostsettings(guildid);
CREATE INDEX IF NOT EXISTS idx_antimasspostignoredroles_settingid ON antimasspostignoredroles(antimasspostsettingid);
CREATE INDEX IF NOT EXISTS idx_antimasspostignoredusers_settingid ON antimasspostignoredusers(antimasspostsettingid);
CREATE INDEX IF NOT EXISTS idx_antimasspostignoredchannels_settingid ON antimasspostignoredchannels(antimasspostsettingid);
CREATE INDEX IF NOT EXISTS idx_antimasspostlinkwhitelist_settingid ON antimasspostlinkwhitelist(antimasspostsettingid);
CREATE INDEX IF NOT EXISTS idx_antimasspostlinkblacklist_settingid ON antimasspostlinkblacklist(antimasspostsettingid);

CREATE INDEX IF NOT EXISTS idx_antipostchannelsettings_guildid ON antipostchannelsettings(guildid);
CREATE INDEX IF NOT EXISTS idx_antipostchannelchannels_settingid ON antipostchannelchannels(antipostchannelsettingid);
CREATE INDEX IF NOT EXISTS idx_antipostchannelignoredroles_settingid ON antipostchannelignoredroles(antipostchannelsettingid);
CREATE INDEX IF NOT EXISTS idx_antipostchannel_ignoredusers_settingid ON antipostchannel_ignoredusers(antipostchannelsettingid);
