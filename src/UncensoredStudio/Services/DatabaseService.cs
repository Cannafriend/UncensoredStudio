using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UncensoredStudio.Models;

namespace UncensoredStudio.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService(string? customDbPath = null)
        {
            if (string.IsNullOrWhiteSpace(customDbPath))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localData = Path.Combine(baseDir, "data");
                string dataDir = Directory.Exists(localData) ? localData : @"I:\UncensoredAI\data";
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }
                _dbPath = Path.Combine(dataDir, "studio_data.db");
            }
            else
            {
                _dbPath = customDbPath;
                string? dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS projects (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    genre TEXT,
                    synopsis TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS chapters (
                    id TEXT PRIMARY KEY,
                    project_id TEXT NOT NULL,
                    title TEXT NOT NULL,
                    order_index INTEGER NOT NULL,
                    content TEXT,
                    notes TEXT,
                    word_count INTEGER DEFAULT 0,
                    updated_at TEXT NOT NULL,
                    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS character_profiles (
                    id TEXT PRIMARY KEY,
                    project_id TEXT,
                    session_id TEXT,
                    name TEXT NOT NULL,
                    aliases TEXT NOT NULL,
                    age_role TEXT,
                    appearance TEXT,
                    personality TEXT,
                    kinks_and_desires TEXT,
                    dialogue_style TEXT,
                    is_enabled INTEGER DEFAULT 1,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS chat_sessions (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    category TEXT NOT NULL,
                    system_prompt TEXT,
                    author_note TEXT,
                    author_note_depth INTEGER DEFAULT 3,
                    perspective TEXT DEFAULT 'Third Person (He/She)',
                    pov_character TEXT DEFAULT 'Omniscient Narrator',
                    temperature REAL DEFAULT 1.05,
                    min_p REAL DEFAULT 0.07,
                    top_p REAL DEFAULT 0.95,
                    repetition_penalty REAL DEFAULT 1.08,
                    dry_multiplier REAL DEFAULT 0.8,
                    dry_base REAL DEFAULT 1.75,
                    dry_allowed_length INTEGER DEFAULT 2,
                    max_tokens INTEGER DEFAULT 1024,
                    context_limit INTEGER DEFAULT 8192,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS chat_messages (
                    id TEXT PRIMARY KEY,
                    session_id TEXT NOT NULL,
                    role TEXT NOT NULL,
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    token_count INTEGER DEFAULT 0,
                    is_pinned INTEGER DEFAULT 0,
                    FOREIGN KEY (session_id) REFERENCES chat_sessions(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS lore_entries (
                    id TEXT PRIMARY KEY,
                    session_id TEXT,
                    title TEXT NOT NULL,
                    keys TEXT NOT NULL,
                    content TEXT NOT NULL,
                    category TEXT NOT NULL,
                    is_enabled INTEGER DEFAULT 1,
                    insertion_order INTEGER DEFAULT 100,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS twitter_drafts (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    category TEXT NOT NULL,
                    raw_content TEXT,
                    tweets_json TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS memory_facts (
                    id TEXT PRIMARY KEY,
                    category TEXT NOT NULL,
                    fact TEXT NOT NULL,
                    is_active INTEGER DEFAULT 1,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS presets (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    description TEXT,
                    system_prompt TEXT,
                    author_note TEXT,
                    temperature REAL,
                    min_p REAL,
                    top_p REAL,
                    repetition_penalty REAL,
                    dry_multiplier REAL
                );

                CREATE TABLE IF NOT EXISTS settings (
                    id TEXT PRIMARY KEY,
                    api_endpoint TEXT,
                    selected_model TEXT,
                    models_directory TEXT,
                    backend_path TEXT,
                    autostart_backend INTEGER,
                    is_first_run_completed INTEGER DEFAULT 0,
                    always_prompt_model_picker INTEGER DEFAULT 1,
                    context_size INTEGER,
                    gpu_layers INTEGER,
                    theme TEXT
                );

                CREATE TABLE IF NOT EXISTS generated_images (
                    id TEXT PRIMARY KEY,
                    file_path TEXT NOT NULL,
                    prompt TEXT NOT NULL,
                    negative_prompt TEXT,
                    model_name TEXT,
                    sampler TEXT,
                    steps INTEGER,
                    cfg_scale REAL,
                    seed INTEGER,
                    width INTEGER,
                    height INTEGER,
                    denoising_strength REAL,
                    is_img2img INTEGER DEFAULT 0,
                    reference_image_path TEXT,
                    created_at TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();

            TryAddColumn(connection, "settings", "models_directory", "TEXT DEFAULT 'I:\\UncensoredAI\\models'");
            TryAddColumn(connection, "settings", "is_first_run_completed", "INTEGER DEFAULT 0");
            TryAddColumn(connection, "settings", "always_prompt_model_picker", "INTEGER DEFAULT 1");
            TryAddColumn(connection, "settings", "selected_font_family", "TEXT DEFAULT 'Segoe UI, sans-serif'");
            TryAddColumn(connection, "settings", "sd_api_endpoint", "TEXT DEFAULT 'http://127.0.0.1:7860'");
            TryAddColumn(connection, "settings", "image_export_directory", "TEXT DEFAULT 'I:\\UncensoredAI\\exports\\images'");
            TryAddColumn(connection, "chat_sessions", "perspective", "TEXT DEFAULT 'Third Person (He/She)'");
            TryAddColumn(connection, "chat_sessions", "pov_character", "TEXT DEFAULT 'Omniscient Narrator'");

            SeedDefaults(connection);
        }

        private void TryAddColumn(SqliteConnection connection, string table, string column, string columnDef)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDef};";
                cmd.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        private void SeedDefaults(SqliteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM settings WHERE id = 'default'";
                long count = (long)(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT INTO settings (id, api_endpoint, selected_model, models_directory, backend_path, autostart_backend, is_first_run_completed, always_prompt_model_picker, context_size, gpu_layers, theme)
                        VALUES ('default', 'http://127.0.0.1:5001', '', 'I:\\UncensoredAI\\models', 'I:\\UncensoredAI\\backend\\koboldcpp.exe', 1, 0, 1, 8192, 99, 'Obsidian');
                    ";
                    insertCmd.ExecuteNonQuery();
                }
            }

            // Seed default character profiles
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM character_profiles";
                long count = (long)(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var insertChar = connection.CreateCommand();
                    insertChar.CommandText = @"
                        INSERT INTO character_profiles (id, project_id, session_id, name, aliases, age_role, appearance, personality, kinks_and_desires, dialogue_style, is_enabled, updated_at)
                        VALUES (@id, NULL, NULL, 'Chaos', 'Chaos, Lord Chaos, The Recluse', '32, Enigmatic Recluse & Tactician',
                                'Tall, athletic lean build, dark tousled hair, sharp jawline, piercing steel-grey eyes. Wears bespoke dark tailored shirts. Scent of cedarwood and amber.',
                                'Intensely perceptive, dominant presence, guarded emotional vulnerability, dry sarcastic wit under tension. Speaks in a low, measured baritone.',
                                'Slow-burn psychological tension, tactile sensory teasing, deep emotional possessiveness, intense physical intimacy.',
                                'Low baritone, deliberate cadence, commanding and intimate.', 1, @now);
                    ";
                    insertChar.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    insertChar.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    insertChar.ExecuteNonQuery();
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM presets";
                long count = (long)(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    var defaultPresets = new List<PromptPreset>
                    {
                        new PromptPreset
                        {
                            Id = "preset_literary_erotica",
                            Name = "Literary Erotica & Slow-Burn",
                            Description = "Rich, sensory-dense prose with deep emotional tension, visceral detail, and zero clichés.",
                            SystemPrompt = "You are an award-winning literary author specializing in evocative, atmospheric, and visceral adult fiction. You write with rich sensory descriptions (scent, touch, temperature, weight, physical arousal, psychological vulnerability), natural dialogue rhythm, and unflinching detail. Show, don't tell. Never sanitize intensity or rush pacing.",
                            AuthorNote = "[Style: slow-burn, third-person limited, visceral sensory descriptions, psychological tension, show-don't-tell]",
                            Temperature = 1.05,
                            MinP = 0.07,
                            TopP = 0.95,
                            RepetitionPenalty = 1.08,
                            DryMultiplier = 0.8
                        },
                        new PromptPreset
                        {
                            Id = "preset_spicy_twitter",
                            Name = "X / Twitter Spicy Teasers & Drabbles",
                            Description = "Punchy, tantalizing hooks, micro-smut, and sultry captions optimized for high engagement.",
                            SystemPrompt = "You are a master of provocative, sultry micro-fiction and high-engagement spicy social copy. You write sharp, teasing, seductive prose that grabs attention in under 280 characters. Balance evocative descriptions with electric dialogue and tantalizing cliffhangers.",
                            AuthorNote = "[Format: punchy, evocative, flirtatious, sharp pacing, provocative hook, Twitter-ready]",
                            Temperature = 1.12,
                            MinP = 0.06,
                            TopP = 0.95,
                            RepetitionPenalty = 1.12,
                            DryMultiplier = 0.85
                        },
                        new PromptPreset
                        {
                            Id = "preset_uncensored_rp",
                            Name = "Uncensored Deep Roleplay",
                            Description = "Dynamic, character-driven storytelling with intense dialogue and immediate physical immersion.",
                            SystemPrompt = "You are an immersive, uncensored roleplay partner and story co-creator. Stay strictly in-character, react authentically to the user's actions with physical reactions, internal monologues, and sharp banter. Push narrative tension forward without moralizing or breaking immersion.",
                            AuthorNote = "[Style: immersive dialogue, expressive body language, authentic character voice, reactive pacing]",
                            Temperature = 1.0,
                            MinP = 0.08,
                            TopP = 0.92,
                            RepetitionPenalty = 1.06,
                            DryMultiplier = 0.75
                        }
                    };

                    foreach (var p in defaultPresets)
                    {
                        using var insertCmd = connection.CreateCommand();
                        insertCmd.CommandText = @"
                            INSERT INTO presets (id, name, description, system_prompt, author_note, temperature, min_p, top_p, repetition_penalty, dry_multiplier)
                            VALUES (@id, @name, @desc, @sys, @an, @temp, @minp, @topp, @rep, @dry);
                        ";
                        insertCmd.Parameters.AddWithValue("@id", p.Id);
                        insertCmd.Parameters.AddWithValue("@name", p.Name);
                        insertCmd.Parameters.AddWithValue("@desc", p.Description);
                        insertCmd.Parameters.AddWithValue("@sys", p.SystemPrompt);
                        insertCmd.Parameters.AddWithValue("@an", p.AuthorNote);
                        insertCmd.Parameters.AddWithValue("@temp", p.Temperature);
                        insertCmd.Parameters.AddWithValue("@minp", p.MinP);
                        insertCmd.Parameters.AddWithValue("@topp", p.TopP);
                        insertCmd.Parameters.AddWithValue("@rep", p.RepetitionPenalty);
                        insertCmd.Parameters.AddWithValue("@dry", p.DryMultiplier);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM projects";
                long count = (long)(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    string projId = Guid.NewGuid().ToString();
                    using var insertProj = connection.CreateCommand();
                    insertProj.CommandText = @"
                        INSERT INTO projects (id, title, genre, synopsis, created_at, updated_at)
                        VALUES (@id, 'Velvet & Shadows', 'Dark Romance / Erotica', 'A wealthy recluse with a dark past crosses paths with an enigmatic rival in a high-stakes underworld of desire and intrigue.', @now, @now);
                    ";
                    insertProj.Parameters.AddWithValue("@id", projId);
                    insertProj.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    insertProj.ExecuteNonQuery();

                    using var insertChap = connection.CreateCommand();
                    insertChap.CommandText = @"
                        INSERT INTO chapters (id, project_id, title, order_index, content, notes, word_count, updated_at)
                        VALUES (@id, @pid, 'Chapter 1: The Invitation', 0, @content, 'Establish the sultry atmosphere and tension between the leads.', 145, @now);
                    ";
                    insertChap.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    insertChap.Parameters.AddWithValue("@pid", projId);
                    insertChap.Parameters.AddWithValue("@content", "The rain against the floor-to-ceiling windows blurred the city lights into streaks of amber and crimson. Inside the penthouse, the silence was heavy, broken only by the quiet clink of amber bourbon swirling in crystal.\n\nChaos watched her from the shadows of the archway. She hadn't heard him enter, or perhaps she was merely pretending—letting the velvet drape of her dress slip slightly as she leaned over the mahogany desk, her fingertips tracing the edge of the unopened seal.");
                    insertChap.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    insertChap.ExecuteNonQuery();
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM lore_entries";
                long count = (long)(cmd.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var insertLore = connection.CreateCommand();
                    insertLore.CommandText = @"
                        INSERT INTO lore_entries (id, session_id, title, keys, content, category, is_enabled, insertion_order, updated_at)
                        VALUES (@id, NULL, 'The Penthouse', 'penthouse, estate, the sanctuary', 'The top-floor luxury penthouse overlooking the city skyline, fitted with dark mahogany, soundproofed glass, private chambers, and vintage art collections.', 'Location', 1, 100, @now);
                    ";
                    insertLore.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    insertLore.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    insertLore.ExecuteNonQuery();
                }
            }
        }

        #region Character Profiles
        public async Task<List<CharacterProfile>> GetCharacterProfilesAsync(string? projectId = null)
        {
            var list = new List<CharacterProfile>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            if (string.IsNullOrEmpty(projectId))
            {
                cmd.CommandText = @"
                    SELECT id, project_id, session_id, name, aliases, age_role, appearance, personality, kinks_and_desires, dialogue_style, is_enabled, updated_at
                    FROM character_profiles
                    ORDER BY name ASC;
                ";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT id, project_id, session_id, name, aliases, age_role, appearance, personality, kinks_and_desires, dialogue_style, is_enabled, updated_at
                    FROM character_profiles
                    WHERE project_id = @pid OR project_id IS NULL
                    ORDER BY name ASC;
                ";
                cmd.Parameters.AddWithValue("@pid", projectId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CharacterProfile
                {
                    Id = reader.GetString(0),
                    ProjectId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    SessionId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Name = reader.GetString(3),
                    Aliases = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    AgeRole = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Appearance = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Personality = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    KinksAndDesires = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    DialogueStyle = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    IsEnabled = reader.GetInt32(10) == 1,
                    UpdatedAt = DateTime.Parse(reader.GetString(11))
                });
            }
            return list;
        }

        public async Task SaveCharacterProfileAsync(CharacterProfile character)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO character_profiles (id, project_id, session_id, name, aliases, age_role, appearance, personality, kinks_and_desires, dialogue_style, is_enabled, updated_at)
                VALUES (@id, @pid, @sid, @name, @aliases, @age, @app, @pers, @kinks, @diag, @enabled, @updated)
                ON CONFLICT(id) DO UPDATE SET
                    project_id = excluded.project_id,
                    session_id = excluded.session_id,
                    name = excluded.name,
                    aliases = excluded.aliases,
                    age_role = excluded.age_role,
                    appearance = excluded.appearance,
                    personality = excluded.personality,
                    kinks_and_desires = excluded.kinks_and_desires,
                    dialogue_style = excluded.dialogue_style,
                    is_enabled = excluded.is_enabled,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@id", character.Id);
            cmd.Parameters.AddWithValue("@pid", (object?)character.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sid", (object?)character.SessionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", character.Name);
            cmd.Parameters.AddWithValue("@aliases", character.Aliases ?? "");
            cmd.Parameters.AddWithValue("@age", character.AgeRole ?? "");
            cmd.Parameters.AddWithValue("@app", character.Appearance ?? "");
            cmd.Parameters.AddWithValue("@pers", character.Personality ?? "");
            cmd.Parameters.AddWithValue("@kinks", character.KinksAndDesires ?? "");
            cmd.Parameters.AddWithValue("@diag", character.DialogueStyle ?? "");
            cmd.Parameters.AddWithValue("@enabled", character.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteCharacterProfileAsync(string characterId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM character_profiles WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", characterId);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Projects & Chapters
        public async Task<List<Project>> GetProjectsAsync()
        {
            var list = new List<Project>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT p.id, p.title, p.genre, p.synopsis, p.created_at, p.updated_at,
                       COALESCE(SUM(c.word_count), 0) as total_words
                FROM projects p
                LEFT JOIN chapters c ON p.id = c.project_id
                GROUP BY p.id
                ORDER BY p.updated_at DESC;
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Project
                {
                    Id = reader.GetString(0),
                    Title = reader.GetString(1),
                    Genre = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Synopsis = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UpdatedAt = DateTime.Parse(reader.GetString(5)),
                    TotalWordCount = reader.GetInt32(6)
                });
            }
            return list;
        }

        public async Task SaveProjectAsync(Project project)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO projects (id, title, genre, synopsis, created_at, updated_at)
                VALUES (@id, @title, @genre, @synopsis, @created_at, @updated_at)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    genre = excluded.genre,
                    synopsis = excluded.synopsis,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@id", project.Id);
            cmd.Parameters.AddWithValue("@title", project.Title);
            cmd.Parameters.AddWithValue("@genre", project.Genre ?? "");
            cmd.Parameters.AddWithValue("@synopsis", project.Synopsis ?? "");
            cmd.Parameters.AddWithValue("@created_at", project.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM projects WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", projectId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<Chapter>> GetChaptersForProjectAsync(string projectId)
        {
            var list = new List<Chapter>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, project_id, title, order_index, content, notes, word_count, updated_at
                FROM chapters
                WHERE project_id = @pid
                ORDER BY order_index ASC;
            ";
            cmd.Parameters.AddWithValue("@pid", projectId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Chapter
                {
                    Id = reader.GetString(0),
                    ProjectId = reader.GetString(1),
                    Title = reader.GetString(2),
                    OrderIndex = reader.GetInt32(3),
                    Content = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Notes = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    WordCount = reader.GetInt32(6),
                    UpdatedAt = DateTime.Parse(reader.GetString(7))
                });
            }
            return list;
        }

        public async Task SaveChapterAsync(Chapter chapter)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO chapters (id, project_id, title, order_index, content, notes, word_count, updated_at)
                VALUES (@id, @pid, @title, @order, @content, @notes, @word_count, @updated_at)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    order_index = excluded.order_index,
                    content = excluded.content,
                    notes = excluded.notes,
                    word_count = excluded.word_count,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@id", chapter.Id);
            cmd.Parameters.AddWithValue("@pid", chapter.ProjectId);
            cmd.Parameters.AddWithValue("@title", chapter.Title);
            cmd.Parameters.AddWithValue("@order", chapter.OrderIndex);
            cmd.Parameters.AddWithValue("@content", chapter.Content ?? "");
            cmd.Parameters.AddWithValue("@notes", chapter.Notes ?? "");
            cmd.Parameters.AddWithValue("@word_count", chapter.WordCount);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();

            using var updateProjCmd = connection.CreateCommand();
            updateProjCmd.CommandText = "UPDATE projects SET updated_at = @now WHERE id = @pid;";
            updateProjCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            updateProjCmd.Parameters.AddWithValue("@pid", chapter.ProjectId);
            await updateProjCmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteChapterAsync(string chapterId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM chapters WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", chapterId);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Chat Sessions & Messages
        public async Task<List<ChatSession>> GetChatSessionsAsync()
        {
            var list = new List<ChatSession>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, title, category, system_prompt, author_note, author_note_depth,
                       perspective, pov_character, temperature, min_p, top_p, repetition_penalty,
                       dry_multiplier, dry_base, dry_allowed_length, max_tokens, context_limit, created_at, updated_at
                FROM chat_sessions
                ORDER BY updated_at DESC;
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ChatSession
                {
                    Id = reader.GetString(0),
                    Title = reader.GetString(1),
                    Category = reader.GetString(2),
                    SystemPrompt = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    AuthorNote = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    AuthorNoteDepth = reader.GetInt32(5),
                    Perspective = reader.IsDBNull(6) ? "Third Person (He/She)" : reader.GetString(6),
                    PovCharacter = reader.IsDBNull(7) ? "Omniscient Narrator" : reader.GetString(7),
                    Temperature = reader.GetDouble(8),
                    MinP = reader.GetDouble(9),
                    TopP = reader.GetDouble(10),
                    RepetitionPenalty = reader.GetDouble(11),
                    DryMultiplier = reader.GetDouble(12),
                    DryBase = reader.GetDouble(13),
                    DryAllowedLength = reader.GetInt32(14),
                    MaxTokens = reader.GetInt32(15),
                    ContextLimit = reader.GetInt32(16),
                    CreatedAt = DateTime.Parse(reader.GetString(17)),
                    UpdatedAt = DateTime.Parse(reader.GetString(18))
                });
            }
            return list;
        }

        public async Task SaveChatSessionAsync(ChatSession session)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO chat_sessions (id, title, category, system_prompt, author_note, author_note_depth,
                                          perspective, pov_character, temperature, min_p, top_p, repetition_penalty,
                                          dry_multiplier, dry_base, dry_allowed_length, max_tokens, context_limit, created_at, updated_at)
                VALUES (@id, @title, @category, @system_prompt, @author_note, @author_note_depth,
                        @perspective, @pov_character, @temperature, @min_p, @top_p, @repetition_penalty,
                        @dry_multiplier, @dry_base, @dry_allowed_length, @max_tokens, @context_limit, @created_at, @updated_at)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    category = excluded.category,
                    system_prompt = excluded.system_prompt,
                    author_note = excluded.author_note,
                    author_note_depth = excluded.author_note_depth,
                    perspective = excluded.perspective,
                    pov_character = excluded.pov_character,
                    temperature = excluded.temperature,
                    min_p = excluded.min_p,
                    top_p = excluded.top_p,
                    repetition_penalty = excluded.repetition_penalty,
                    dry_multiplier = excluded.dry_multiplier,
                    dry_base = excluded.dry_base,
                    dry_allowed_length = excluded.dry_allowed_length,
                    max_tokens = excluded.max_tokens,
                    context_limit = excluded.context_limit,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@id", session.Id);
            cmd.Parameters.AddWithValue("@title", session.Title);
            cmd.Parameters.AddWithValue("@category", session.Category);
            cmd.Parameters.AddWithValue("@system_prompt", session.SystemPrompt ?? "");
            cmd.Parameters.AddWithValue("@author_note", session.AuthorNote ?? "");
            cmd.Parameters.AddWithValue("@author_note_depth", session.AuthorNoteDepth);
            cmd.Parameters.AddWithValue("@perspective", session.Perspective ?? "Third Person (He/She)");
            cmd.Parameters.AddWithValue("@pov_character", session.PovCharacter ?? "Omniscient Narrator");
            cmd.Parameters.AddWithValue("@temperature", session.Temperature);
            cmd.Parameters.AddWithValue("@min_p", session.MinP);
            cmd.Parameters.AddWithValue("@top_p", session.TopP);
            cmd.Parameters.AddWithValue("@repetition_penalty", session.RepetitionPenalty);
            cmd.Parameters.AddWithValue("@dry_multiplier", session.DryMultiplier);
            cmd.Parameters.AddWithValue("@dry_base", session.DryBase);
            cmd.Parameters.AddWithValue("@dry_allowed_length", session.DryAllowedLength);
            cmd.Parameters.AddWithValue("@max_tokens", session.MaxTokens);
            cmd.Parameters.AddWithValue("@context_limit", session.ContextLimit);
            cmd.Parameters.AddWithValue("@created_at", session.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteChatSessionAsync(string sessionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM chat_sessions WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", sessionId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<ChatMessage>> GetMessagesForSessionAsync(string sessionId)
        {
            var list = new List<ChatMessage>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, session_id, role, content, created_at, token_count, is_pinned
                FROM chat_messages
                WHERE session_id = @sid
                ORDER BY created_at ASC;
            ";
            cmd.Parameters.AddWithValue("@sid", sessionId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ChatMessage
                {
                    Id = reader.GetString(0),
                    SessionId = reader.GetString(1),
                    Role = reader.GetString(2),
                    Content = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    TokenCount = reader.GetInt32(5),
                    IsPinned = reader.GetInt32(6) == 1
                });
            }
            return list;
        }

        public async Task SaveMessageAsync(ChatMessage message)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO chat_messages (id, session_id, role, content, created_at, token_count, is_pinned)
                VALUES (@id, @sid, @role, @content, @created_at, @token_count, @is_pinned)
                ON CONFLICT(id) DO UPDATE SET
                    content = excluded.content,
                    token_count = excluded.token_count,
                    is_pinned = excluded.is_pinned;
            ";
            cmd.Parameters.AddWithValue("@id", message.Id);
            cmd.Parameters.AddWithValue("@sid", message.SessionId);
            cmd.Parameters.AddWithValue("@role", message.Role);
            cmd.Parameters.AddWithValue("@content", message.Content ?? "");
            cmd.Parameters.AddWithValue("@created_at", message.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@token_count", message.TokenCount);
            cmd.Parameters.AddWithValue("@is_pinned", message.IsPinned ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();

            using var updateSessionCmd = connection.CreateCommand();
            updateSessionCmd.CommandText = "UPDATE chat_sessions SET updated_at = @now WHERE id = @sid;";
            updateSessionCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            updateSessionCmd.Parameters.AddWithValue("@sid", message.SessionId);
            await updateSessionCmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteMessageAsync(string messageId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM chat_messages WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", messageId);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Lorebook Entries
        public async Task<List<LoreEntry>> GetLoreEntriesAsync(string? sessionId = null)
        {
            var list = new List<LoreEntry>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            if (string.IsNullOrEmpty(sessionId))
            {
                cmd.CommandText = @"
                    SELECT id, session_id, title, keys, content, category, is_enabled, insertion_order, updated_at
                    FROM lore_entries
                    ORDER BY category ASC, title ASC;
                ";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT id, session_id, title, keys, content, category, is_enabled, insertion_order, updated_at
                    FROM lore_entries
                    WHERE session_id = @sid OR session_id IS NULL
                    ORDER BY insertion_order ASC, title ASC;
                ";
                cmd.Parameters.AddWithValue("@sid", sessionId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LoreEntry
                {
                    Id = reader.GetString(0),
                    SessionId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Title = reader.GetString(2),
                    Keys = reader.GetString(3),
                    Content = reader.GetString(4),
                    Category = reader.GetString(5),
                    IsEnabled = reader.GetInt32(6) == 1,
                    InsertionOrder = reader.GetInt32(7),
                    UpdatedAt = DateTime.Parse(reader.GetString(8))
                });
            }
            return list;
        }

        public async Task SaveLoreEntryAsync(LoreEntry entry)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO lore_entries (id, session_id, title, keys, content, category, is_enabled, insertion_order, updated_at)
                VALUES (@id, @sid, @title, @keys, @content, @category, @is_enabled, @order, @updated_at)
                ON CONFLICT(id) DO UPDATE SET
                    session_id = excluded.session_id,
                    title = excluded.title,
                    keys = excluded.keys,
                    content = excluded.content,
                    category = excluded.category,
                    is_enabled = excluded.is_enabled,
                    insertion_order = excluded.insertion_order,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@id", entry.Id);
            cmd.Parameters.AddWithValue("@sid", (object?)entry.SessionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title", entry.Title);
            cmd.Parameters.AddWithValue("@keys", entry.Keys ?? "");
            cmd.Parameters.AddWithValue("@content", entry.Content ?? "");
            cmd.Parameters.AddWithValue("@category", entry.Category ?? "Lore");
            cmd.Parameters.AddWithValue("@is_enabled", entry.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@order", entry.InsertionOrder);
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteLoreEntryAsync(string entryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM lore_entries WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", entryId);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Twitter Drafts
        public async Task<List<TwitterDraft>> GetTwitterDraftsAsync()
        {
            var list = new List<TwitterDraft>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, title, category, raw_content, tweets_json, created_at, updated_at
                FROM twitter_drafts
                ORDER BY updated_at DESC;
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TwitterDraft
                {
                    Id = reader.GetString(0),
                    Title = reader.GetString(1),
                    Category = reader.GetString(2),
                    RawContent = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    TweetsJson = reader.IsDBNull(4) ? "[]" : reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    UpdatedAt = DateTime.Parse(reader.GetString(6))
                });
            }
            return list;
        }

        public async Task SaveTwitterDraftAsync(TwitterDraft draft)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO twitter_drafts (id, title, category, raw_content, tweets_json, created_at, updated_at)
                VALUES (@id, @title, @category, @raw_content, @tweets_json, @created_at, @updated_at)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    category = excluded.category,
                    raw_content = excluded.raw_content,
                    tweets_json = excluded.tweets_json,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@id", draft.Id);
            cmd.Parameters.AddWithValue("@title", draft.Title);
            cmd.Parameters.AddWithValue("@category", draft.Category);
            cmd.Parameters.AddWithValue("@raw_content", draft.RawContent ?? "");
            cmd.Parameters.AddWithValue("@tweets_json", draft.TweetsJson ?? "[]");
            cmd.Parameters.AddWithValue("@created_at", draft.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteTwitterDraftAsync(string draftId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM twitter_drafts WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", draftId);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Persistent Memory Facts
        public async Task<List<MemoryFact>> GetMemoryFactsAsync()
        {
            var list = new List<MemoryFact>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, category, fact, is_active, created_at FROM memory_facts ORDER BY created_at DESC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new MemoryFact
                {
                    Id = reader.GetString(0),
                    Category = reader.GetString(1),
                    Fact = reader.GetString(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                });
            }
            return list;
        }

        public async Task SaveMemoryFactAsync(MemoryFact memory)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO memory_facts (id, category, fact, is_active, created_at)
                VALUES (@id, @category, @fact, @active, @created)
                ON CONFLICT(id) DO UPDATE SET
                    category = excluded.category,
                    fact = excluded.fact,
                    is_active = excluded.is_active;
            ";
            cmd.Parameters.AddWithValue("@id", memory.Id);
            cmd.Parameters.AddWithValue("@category", memory.Category ?? "General");
            cmd.Parameters.AddWithValue("@fact", memory.Fact ?? "");
            cmd.Parameters.AddWithValue("@active", memory.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@created", memory.CreatedAt.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteMemoryFactAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM memory_facts WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region App Settings & Presets
        public async Task<AppSettings> GetSettingsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, api_endpoint, selected_model, models_directory, backend_path, autostart_backend, is_first_run_completed, always_prompt_model_picker, context_size, gpu_layers, theme, selected_font_family
                FROM settings
                WHERE id = 'default';
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AppSettings
                {
                    Id = reader.GetString(0),
                    ApiEndpoint = reader.IsDBNull(1) ? "http://127.0.0.1:5001" : reader.GetString(1),
                    SelectedModel = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ModelsDirectory = reader.IsDBNull(3) ? @"I:\UncensoredAI\models" : reader.GetString(3),
                    BackendPath = reader.IsDBNull(4) ? @"I:\UncensoredAI\backend\koboldcpp.exe" : reader.GetString(4),
                    AutoStartBackend = reader.GetInt32(5) == 1,
                    IsFirstRunCompleted = reader.GetInt32(6) == 1,
                    AlwaysPromptModelPicker = reader.GetInt32(7) == 1,
                    ContextSize = reader.GetInt32(8),
                    GpuLayers = reader.GetInt32(9),
                    Theme = reader.IsDBNull(10) ? "Obsidian" : reader.GetString(10),
                    SelectedFontFamily = reader.IsDBNull(11) ? "Segoe UI, sans-serif" : reader.GetString(11)
                };
            }
            return new AppSettings();
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO settings (id, api_endpoint, selected_model, models_directory, backend_path, autostart_backend, is_first_run_completed, always_prompt_model_picker, context_size, gpu_layers, theme, selected_font_family)
                VALUES ('default', @api, @model, @modelsDir, @backend, @autostart, @firstRun, @promptPicker, @ctx, @gpu, @theme, @font)
                ON CONFLICT(id) DO UPDATE SET
                    api_endpoint = excluded.api_endpoint,
                    selected_model = excluded.selected_model,
                    models_directory = excluded.models_directory,
                    backend_path = excluded.backend_path,
                    autostart_backend = excluded.autostart_backend,
                    is_first_run_completed = excluded.is_first_run_completed,
                    always_prompt_model_picker = excluded.always_prompt_model_picker,
                    context_size = excluded.context_size,
                    gpu_layers = excluded.gpu_layers,
                    theme = excluded.theme,
                    selected_font_family = excluded.selected_font_family;
            ";
            cmd.Parameters.AddWithValue("@api", settings.ApiEndpoint);
            cmd.Parameters.AddWithValue("@model", settings.SelectedModel ?? "");
            cmd.Parameters.AddWithValue("@modelsDir", settings.ModelsDirectory ?? @"I:\UncensoredAI\models");
            cmd.Parameters.AddWithValue("@backend", settings.BackendPath ?? "");
            cmd.Parameters.AddWithValue("@autostart", settings.AutoStartBackend ? 1 : 0);
            cmd.Parameters.AddWithValue("@firstRun", settings.IsFirstRunCompleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@promptPicker", settings.AlwaysPromptModelPicker ? 1 : 0);
            cmd.Parameters.AddWithValue("@ctx", settings.ContextSize);
            cmd.Parameters.AddWithValue("@gpu", settings.GpuLayers);
            cmd.Parameters.AddWithValue("@theme", settings.Theme ?? "Obsidian");
            cmd.Parameters.AddWithValue("@font", settings.SelectedFontFamily ?? "Segoe UI, sans-serif");
            await cmd.ExecuteNonQueryAsync();
        }

        #region Memory Wipe & Reset Suite
        public async Task ClearAllStudioMemoryAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM chat_messages;
                DELETE FROM chat_sessions;
                DELETE FROM lore_entries;
                DELETE FROM character_profiles;
                DELETE FROM chapters;
                DELETE FROM projects;
                DELETE FROM twitter_drafts;
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ClearStoryMemoryAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM chapters WHERE project_id = @pid;
                DELETE FROM character_profiles WHERE project_id = @pid;
                UPDATE projects SET updated_at = @now WHERE id = @pid;
            ";
            cmd.Parameters.AddWithValue("@pid", projectId);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ClearAllChatHistoryAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM chat_messages;";
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ClearAllLoreAndCharactersAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM lore_entries;
                DELETE FROM character_profiles;
            ";
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        public async Task<List<PromptPreset>> GetPresetsAsync()
        {
            var list = new List<PromptPreset>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, description, system_prompt, author_note, temperature, min_p, top_p, repetition_penalty, dry_multiplier FROM presets ORDER BY name ASC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PromptPreset
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SystemPrompt = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    AuthorNote = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Temperature = reader.GetDouble(5),
                    MinP = reader.GetDouble(6),
                    TopP = reader.GetDouble(7),
                    RepetitionPenalty = reader.GetDouble(8),
                    DryMultiplier = reader.GetDouble(9)
                });
            }
            return list;
        }
        #region Generated Images Gallery
        public async Task<List<GeneratedImageItem>> GetGeneratedImagesAsync()
        {
            var list = new List<GeneratedImageItem>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, file_path, prompt, negative_prompt, model_name, sampler, steps, cfg_scale, seed, width, height, denoising_strength, is_img2img, reference_image_path, created_at
                FROM generated_images
                ORDER BY created_at DESC;
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new GeneratedImageItem
                {
                    Id = reader.GetString(0),
                    FilePath = reader.GetString(1),
                    Prompt = reader.GetString(2),
                    NegativePrompt = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ModelName = reader.IsDBNull(4) ? "SDXL / Pony" : reader.GetString(4),
                    Sampler = reader.IsDBNull(5) ? "DPM++ 2M Karras" : reader.GetString(5),
                    Steps = reader.GetInt32(6),
                    CfgScale = reader.GetDouble(7),
                    Seed = reader.GetInt64(8),
                    Width = reader.GetInt32(9),
                    Height = reader.GetInt32(10),
                    DenoisingStrength = reader.GetDouble(11),
                    IsImg2Img = reader.GetInt32(12) == 1,
                    ReferenceImagePath = reader.IsDBNull(13) ? null : reader.GetString(13),
                    CreatedAt = DateTime.Parse(reader.GetString(14))
                });
            }
            return list;
        }

        public async Task SaveGeneratedImageAsync(GeneratedImageItem item)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO generated_images (id, file_path, prompt, negative_prompt, model_name, sampler, steps, cfg_scale, seed, width, height, denoising_strength, is_img2img, reference_image_path, created_at)
                VALUES (@id, @file, @prompt, @neg, @model, @sampler, @steps, @cfg, @seed, @w, @h, @denoise, @img2img, @ref, @created)
                ON CONFLICT(id) DO UPDATE SET
                    file_path = excluded.file_path,
                    prompt = excluded.prompt,
                    negative_prompt = excluded.negative_prompt,
                    model_name = excluded.model_name,
                    sampler = excluded.sampler,
                    steps = excluded.steps,
                    cfg_scale = excluded.cfg_scale,
                    seed = excluded.seed,
                    width = excluded.width,
                    height = excluded.height,
                    denoising_strength = excluded.denoising_strength,
                    is_img2img = excluded.is_img2img,
                    reference_image_path = excluded.reference_image_path;
            ";
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@file", item.FilePath);
            cmd.Parameters.AddWithValue("@prompt", item.Prompt ?? "");
            cmd.Parameters.AddWithValue("@neg", item.NegativePrompt ?? "");
            cmd.Parameters.AddWithValue("@model", item.ModelName ?? "SDXL / Pony");
            cmd.Parameters.AddWithValue("@sampler", item.Sampler ?? "DPM++ 2M Karras");
            cmd.Parameters.AddWithValue("@steps", item.Steps);
            cmd.Parameters.AddWithValue("@cfg", item.CfgScale);
            cmd.Parameters.AddWithValue("@seed", item.Seed);
            cmd.Parameters.AddWithValue("@w", item.Width);
            cmd.Parameters.AddWithValue("@h", item.Height);
            cmd.Parameters.AddWithValue("@denoise", item.DenoisingStrength);
            cmd.Parameters.AddWithValue("@img2img", item.IsImg2Img ? 1 : 0);
            cmd.Parameters.AddWithValue("@ref", (object?)item.ReferenceImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", item.CreatedAt.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteGeneratedImageAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM generated_images WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion
        #endregion
    }
}
