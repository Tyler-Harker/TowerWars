-- TowerWars Database Schema

-- Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username VARCHAR(32) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_login_at TIMESTAMP WITH TIME ZONE,
    banned_until TIMESTAMP WITH TIME ZONE,
    ban_reason TEXT
);

CREATE INDEX idx_users_username ON users(username);
CREATE INDEX idx_users_email ON users(email);

-- Sessions table
CREATE TABLE sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL,
    refresh_token_hash VARCHAR(255),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    ip_address INET,
    user_agent TEXT
);

CREATE INDEX idx_sessions_user_id ON sessions(user_id);
CREATE INDEX idx_sessions_token_hash ON sessions(token_hash);
CREATE INDEX idx_sessions_expires_at ON sessions(expires_at);

-- Characters table
CREATE TABLE characters (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(32) NOT NULL,
    class VARCHAR(32) NOT NULL,
    level INTEGER DEFAULT 1,
    experience BIGINT DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    deleted_at TIMESTAMP WITH TIME ZONE,
    UNIQUE(user_id, name)
);

CREATE INDEX idx_characters_user_id ON characters(user_id);

-- Character inventory
CREATE TABLE character_inventory (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    character_id UUID NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
    item_type VARCHAR(64) NOT NULL,
    item_data JSONB,
    quantity INTEGER DEFAULT 1,
    acquired_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_character_inventory_character_id ON character_inventory(character_id);

-- Player stats (aggregate stats per user)
CREATE TABLE player_stats (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    wins INTEGER DEFAULT 0,
    losses INTEGER DEFAULT 0,
    elo_rating INTEGER DEFAULT 1000,
    highest_wave_solo INTEGER DEFAULT 0,
    total_units_killed BIGINT DEFAULT 0,
    total_towers_built BIGINT DEFAULT 0,
    total_gold_earned BIGINT DEFAULT 0,
    total_play_time_seconds BIGINT DEFAULT 0,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Match history
CREATE TABLE matches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    mode VARCHAR(16) NOT NULL,
    map_id VARCHAR(64),
    result VARCHAR(16),
    waves_completed INTEGER DEFAULT 0,
    duration_seconds REAL,
    started_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    ended_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX idx_matches_ended_at ON matches(ended_at DESC);
CREATE INDEX idx_matches_mode ON matches(mode);

-- Match participants
CREATE TABLE match_participants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    match_id UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    character_id UUID REFERENCES characters(id) ON DELETE SET NULL,
    team_id SMALLINT,
    score INTEGER DEFAULT 0,
    units_killed INTEGER DEFAULT 0,
    towers_built INTEGER DEFAULT 0,
    gold_earned INTEGER DEFAULT 0,
    damage_dealt INTEGER DEFAULT 0,
    lives_lost INTEGER DEFAULT 0,
    result VARCHAR(16),
    elo_change INTEGER DEFAULT 0
);

CREATE INDEX idx_match_participants_match_id ON match_participants(match_id);
CREATE INDEX idx_match_participants_user_id ON match_participants(user_id);

-- Friends table
CREATE TABLE friends (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    friend_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    status VARCHAR(16) NOT NULL DEFAULT 'pending',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(user_id, friend_id),
    CHECK (user_id != friend_id)
);

CREATE INDEX idx_friends_user_id ON friends(user_id);
CREATE INDEX idx_friends_friend_id ON friends(friend_id);
CREATE INDEX idx_friends_status ON friends(status);

-- Chat messages (for persistence, recent messages also in Redis)
CREATE TABLE chat_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    channel VARCHAR(32) NOT NULL,
    sender_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    recipient_id UUID REFERENCES users(id) ON DELETE CASCADE,
    content TEXT NOT NULL,
    sent_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_chat_messages_channel ON chat_messages(channel, sent_at DESC);
CREATE INDEX idx_chat_messages_sender_id ON chat_messages(sender_id);
CREATE INDEX idx_chat_messages_recipient_id ON chat_messages(recipient_id);

-- Match events (for replay/event sourcing)
CREATE TABLE match_events (
    id BIGSERIAL PRIMARY KEY,
    match_id UUID NOT NULL,
    event_type VARCHAR(64) NOT NULL,
    event_data JSONB NOT NULL,
    tick INTEGER,
    occurred_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_match_events_match_id ON match_events(match_id, id);
CREATE INDEX idx_match_events_type ON match_events(event_type);

-- Audit log for important actions
CREATE TABLE audit_log (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    action VARCHAR(64) NOT NULL,
    details JSONB,
    ip_address INET,
    occurred_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_audit_log_user_id ON audit_log(user_id);
CREATE INDEX idx_audit_log_action ON audit_log(action);
CREATE INDEX idx_audit_log_occurred_at ON audit_log(occurred_at DESC);

-- ============================================================================
-- TOWER PROGRESSION SYSTEM
-- ============================================================================

-- Add inventory slots to users
ALTER TABLE users ADD COLUMN inventory_slots INTEGER DEFAULT 50;

-- Player tower progression (one per tower type per user)
CREATE TABLE player_towers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tower_type SMALLINT NOT NULL,
    experience BIGINT DEFAULT 0,
    level INTEGER DEFAULT 1,
    unlocked BOOLEAN DEFAULT FALSE,
    unlocked_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(user_id, tower_type)
);

CREATE INDEX idx_player_towers_user_id ON player_towers(user_id);

-- Skill tree node definitions (static, seeded)
CREATE TABLE tower_skill_nodes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tower_type SMALLINT NOT NULL,
    node_id VARCHAR(32) NOT NULL,
    tier SMALLINT NOT NULL,
    position_x SMALLINT NOT NULL,
    position_y SMALLINT NOT NULL,
    name VARCHAR(64) NOT NULL,
    description TEXT NOT NULL,
    skill_points_cost SMALLINT DEFAULT 1,
    required_tower_level INTEGER DEFAULT 1,
    bonus_type VARCHAR(32) NOT NULL,
    bonus_value DECIMAL(10, 4) NOT NULL,
    bonus_value_per_rank DECIMAL(10, 4) DEFAULT 0,
    max_ranks SMALLINT DEFAULT 1,
    prerequisite_node_ids TEXT[],
    UNIQUE(tower_type, node_id)
);

CREATE INDEX idx_tower_skill_nodes_tower_type ON tower_skill_nodes(tower_type);

-- Player's allocated skill points
CREATE TABLE player_tower_skills (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    player_tower_id UUID NOT NULL REFERENCES player_towers(id) ON DELETE CASCADE,
    skill_node_id UUID NOT NULL REFERENCES tower_skill_nodes(id),
    ranks_allocated SMALLINT DEFAULT 1,
    allocated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(player_tower_id, skill_node_id)
);

CREATE INDEX idx_player_tower_skills_player_tower_id ON player_tower_skills(player_tower_id);

-- ============================================================================
-- EQUIPMENT SYSTEM
-- ============================================================================

-- Item base definitions (static, seeded)
CREATE TABLE item_bases (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(64) NOT NULL,
    item_type SMALLINT NOT NULL, -- 0=weapon, 1=shield, 2=accessory
    weapon_subtype SMALLINT, -- 0=bow, 1=sword, 2=club, 3=wand, 4=axe
    accessory_subtype SMALLINT, -- 0=ring, 1=amulet, 2=charm
    base_damage INTEGER,
    base_range DECIMAL(4,2),
    base_attack_speed DECIMAL(4,2),
    hits_multiple BOOLEAN DEFAULT FALSE,
    max_targets INTEGER DEFAULT 1,
    base_hp_bonus INTEGER DEFAULT 0,
    base_block_chance DECIMAL(4,2) DEFAULT 0,
    required_tower_level INTEGER DEFAULT 1,
    icon VARCHAR(64),
    UNIQUE(name)
);

-- Affix definitions (static, seeded)
CREATE TABLE item_affixes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(64) NOT NULL,
    display_template VARCHAR(128) NOT NULL,
    affix_type SMALLINT NOT NULL, -- 0=prefix, 1=suffix
    bonus_type VARCHAR(32) NOT NULL,
    min_value DECIMAL(10,4) NOT NULL,
    max_value DECIMAL(10,4) NOT NULL,
    weight INTEGER DEFAULT 100,
    allowed_item_types SMALLINT[],
    UNIQUE(name)
);

-- Player inventory (rolled items with affixes)
CREATE TABLE player_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    item_base_id UUID NOT NULL REFERENCES item_bases(id),
    rarity SMALLINT NOT NULL, -- 0=normal, 1=magic, 2=rare
    affixes JSONB DEFAULT '[]',
    obtained_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    obtained_from VARCHAR(32),
    match_id UUID
);

CREATE INDEX idx_player_items_user_id ON player_items(user_id);

-- Equipped items per tower (per slot)
CREATE TABLE player_tower_equipment (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    player_tower_id UUID NOT NULL REFERENCES player_towers(id) ON DELETE CASCADE,
    slot SMALLINT NOT NULL, -- 0=weapon, 1=shield, 2/3/4=accessory
    item_id UUID REFERENCES player_items(id) ON DELETE SET NULL,
    UNIQUE(player_tower_id, slot),
    UNIQUE(item_id)
);

CREATE INDEX idx_player_tower_equipment_player_tower_id ON player_tower_equipment(player_tower_id);

-- ============================================================================
-- SEED DATA: ITEM BASES
-- ============================================================================

-- Weapons
INSERT INTO item_bases (id, name, item_type, weapon_subtype, base_damage, base_range, base_attack_speed, hits_multiple, max_targets, required_tower_level) VALUES
    (uuid_generate_v4(), 'Longbow', 0, 0, 12, 6.0, 1.5, FALSE, 1, 1),
    (uuid_generate_v4(), 'Short Bow', 0, 0, 8, 5.0, 2.0, FALSE, 1, 1),
    (uuid_generate_v4(), 'Crossbow', 0, 0, 18, 5.5, 1.0, FALSE, 1, 3),
    (uuid_generate_v4(), 'Broadsword', 0, 1, 25, 2.0, 1.0, TRUE, 3, 1),
    (uuid_generate_v4(), 'Greatsword', 0, 1, 40, 2.5, 0.7, TRUE, 4, 5),
    (uuid_generate_v4(), 'Rapier', 0, 1, 15, 1.8, 1.5, TRUE, 2, 2),
    (uuid_generate_v4(), 'War Club', 0, 2, 50, 1.5, 0.4, FALSE, 1, 1),
    (uuid_generate_v4(), 'Mace', 0, 2, 35, 1.8, 0.6, FALSE, 1, 3),
    (uuid_generate_v4(), 'Hammer', 0, 2, 70, 1.5, 0.3, FALSE, 1, 7),
    (uuid_generate_v4(), 'Oak Wand', 0, 3, 15, 4.5, 0.8, FALSE, 1, 1),
    (uuid_generate_v4(), 'Crystal Wand', 0, 3, 22, 5.0, 0.9, FALSE, 1, 4),
    (uuid_generate_v4(), 'Staff', 0, 3, 30, 5.5, 0.7, FALSE, 1, 6),
    (uuid_generate_v4(), 'Throwing Axe', 0, 4, 20, 4.0, 0.6, FALSE, 1, 1),
    (uuid_generate_v4(), 'Battle Axe', 0, 4, 35, 4.5, 0.5, FALSE, 1, 4),
    (uuid_generate_v4(), 'Tomahawk', 0, 4, 28, 5.0, 0.7, FALSE, 1, 5);

-- Shields
INSERT INTO item_bases (id, name, item_type, base_hp_bonus, base_block_chance, required_tower_level) VALUES
    (uuid_generate_v4(), 'Wooden Shield', 1, 50, 5.0, 1),
    (uuid_generate_v4(), 'Iron Shield', 1, 100, 8.0, 3),
    (uuid_generate_v4(), 'Tower Shield', 1, 150, 12.0, 5),
    (uuid_generate_v4(), 'Kite Shield', 1, 80, 10.0, 4),
    (uuid_generate_v4(), 'Buckler', 1, 30, 15.0, 2);

-- Accessories - Rings (offensive)
INSERT INTO item_bases (id, name, item_type, accessory_subtype, required_tower_level) VALUES
    (uuid_generate_v4(), 'Iron Ring', 2, 0, 1),
    (uuid_generate_v4(), 'Gold Ring', 2, 0, 3),
    (uuid_generate_v4(), 'Ruby Ring', 2, 0, 5),
    (uuid_generate_v4(), 'Diamond Ring', 2, 0, 8);

-- Accessories - Amulets (utility)
INSERT INTO item_bases (id, name, item_type, accessory_subtype, required_tower_level) VALUES
    (uuid_generate_v4(), 'Bronze Amulet', 2, 1, 1),
    (uuid_generate_v4(), 'Silver Amulet', 2, 1, 3),
    (uuid_generate_v4(), 'Gold Amulet', 2, 1, 5),
    (uuid_generate_v4(), 'Jade Amulet', 2, 1, 7);

-- Accessories - Charms (elemental)
INSERT INTO item_bases (id, name, item_type, accessory_subtype, required_tower_level) VALUES
    (uuid_generate_v4(), 'Fire Charm', 2, 2, 2),
    (uuid_generate_v4(), 'Ice Charm', 2, 2, 2),
    (uuid_generate_v4(), 'Lightning Charm', 2, 2, 2),
    (uuid_generate_v4(), 'Poison Charm', 2, 2, 2);

-- ============================================================================
-- SEED DATA: ITEM AFFIXES
-- ============================================================================

-- Prefix affixes (offensive)
INSERT INTO item_affixes (id, name, display_template, affix_type, bonus_type, min_value, max_value, weight, allowed_item_types) VALUES
    (uuid_generate_v4(), 'Damaging', '+{value}% Damage', 0, 'DamagePercent', 5, 15, 100, ARRAY[0,1,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Rapid', '+{value}% Attack Speed', 0, 'AttackSpeedPercent', 5, 15, 100, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Far-reaching', '+{value}% Range', 0, 'RangePercent', 5, 15, 100, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Precise', '+{value}% Critical Chance', 0, 'CritChance', 2, 8, 80, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Flaming', '+{value} Fire Damage', 0, 'FireDamageFlat', 3, 10, 60, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Freezing', '+{value} Ice Damage', 0, 'IceDamageFlat', 3, 10, 60, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Shocking', '+{value} Lightning Damage', 0, 'LightningDamageFlat', 3, 10, 60, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Toxic', '+{value} Poison Damage', 0, 'PoisonDamageFlat', 3, 10, 60, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'Sturdy', '+{value} Tower HP', 0, 'TowerHpFlat', 30, 100, 80, ARRAY[1]::SMALLINT[]),
    (uuid_generate_v4(), 'Reinforced', '+{value}% Tower HP', 0, 'TowerHpPercent', 5, 15, 60, ARRAY[1]::SMALLINT[]);

-- Suffix affixes (defensive/utility)
INSERT INTO item_affixes (id, name, display_template, affix_type, bonus_type, min_value, max_value, weight, allowed_item_types) VALUES
    (uuid_generate_v4(), 'of Destruction', '+{value}% Critical Damage', 1, 'CritMultiplier', 15, 50, 70, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Leeching', '{value}% Life Leech', 1, 'LifeLeechPercent', 1, 3, 40, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Wealth', '+{value}% Gold Find', 1, 'GoldFindPercent', 5, 20, 50, ARRAY[0,1,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Learning', '+{value}% XP Gain', 1, 'XpGainPercent', 5, 20, 50, ARRAY[0,1,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Fortitude', '+{value} Tower HP', 1, 'TowerHpFlat', 50, 200, 80, ARRAY[1]::SMALLINT[]),
    (uuid_generate_v4(), 'of Warding', '+{value}% Damage Reduction', 1, 'DamageReductionPercent', 5, 15, 60, ARRAY[1,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Flames', '+{value}% Fire Damage', 1, 'FireDamagePercent', 5, 15, 50, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Frost', '+{value}% Ice Damage', 1, 'IceDamagePercent', 5, 15, 50, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Thunder', '+{value}% Lightning Damage', 1, 'LightningDamagePercent', 5, 15, 50, ARRAY[0,2]::SMALLINT[]),
    (uuid_generate_v4(), 'of Venom', '+{value}% Poison Damage', 1, 'PoisonDamagePercent', 5, 15, 50, ARRAY[0,2]::SMALLINT[]);
