using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UncensoredStudio.Models;

namespace UncensoredStudio.Services
{
    public class ContextBuilderService
    {
        private readonly DatabaseService _databaseService;

        public ContextBuilderService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<ChatMessage>> BuildIsolatedChatContextAsync(
            ChatSession session,
            List<ChatMessage> existingMessages,
            string? newUserPrompt = null)
        {
            var finalMessages = new List<ChatMessage>();

            bool isAssistantMode = session.Category.Contains("Assistant", StringComparison.OrdinalIgnoreCase) || 
                                   session.Category.Contains("General", StringComparison.OrdinalIgnoreCase) || 
                                   session.Category.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
                                   session.Category.Contains("Reasoning", StringComparison.OrdinalIgnoreCase);

            var triggeredChars = new List<CharacterProfile>();
            var triggeredLore = new List<LoreEntry>();

            var fullContextText = new StringBuilder();
            if (!string.IsNullOrEmpty(newUserPrompt))
            {
                fullContextText.AppendLine(newUserPrompt);
            }
            foreach (var m in existingMessages.TakeLast(6))
            {
                fullContextText.AppendLine(m.Content);
            }
            string scanText = fullContextText.ToString();

            if (!isAssistantMode)
            {
                // 1. Fetch Character Profiles & Lorebook only for Story/Roleplay mode
                var allCharacters = await _databaseService.GetCharacterProfilesAsync();
                var allLore = await _databaseService.GetLoreEntriesAsync(session.Id);

                triggeredChars = GetTriggeredCharacters(allCharacters.Where(c => c.IsEnabled).ToList(), scanText);
                triggeredLore = GetTriggeredLoreEntries(allLore.Where(l => l.IsEnabled).ToList(), scanText);
            }
            else
            {
                // 1. In AI Assistant / Coding / Reasoning mode, scan for Technical Skill Lorebooks
                var allLore = await _databaseService.GetLoreEntriesAsync(session.Id);
                var skillLore = allLore.Where(l => l.IsEnabled && !l.Category.Equals("Character", StringComparison.OrdinalIgnoreCase)).ToList();

                triggeredLore = GetTriggeredLoreEntries(skillLore, scanText);
            }

            // 2. Assemble System Prompt with Cognition Directives
            var systemContent = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(session.SystemPrompt))
            {
                systemContent.AppendLine(session.SystemPrompt.Trim());
            }

            // Inject Active Persistent Working Memory Facts (Active across all sessions)
            var activeMemories = await _databaseService.GetMemoryFactsAsync();
            var enabledMemories = activeMemories.Where(m => m.IsActive).ToList();
            if (enabledMemories.Count > 0)
            {
                systemContent.AppendLine("\n[PERSISTENT WORKING MEMORY & USER CONTEXT]:");
                foreach (var mem in enabledMemories)
                {
                    systemContent.AppendLine($"• [{mem.Category}]: {mem.Fact}");
                }
            }

            if (isAssistantMode)
            {
                systemContent.AppendLine("\n[CORE ASSISTANT OPERATIONAL DIRECTIVES]:");
                systemContent.AppendLine("1. PURE INTELLIGENT ASSISTANT: Act as a direct, highly capable AI assistant. Answer user questions, solve problems, review code, or reason through queries directly. Do NOT default to writing fictional story prose unless explicitly asked by the user.");
                systemContent.AppendLine("2. STRICT FACTUAL ADHERENCE: Never fabricate libraries, fake citations, or synthetic claims. If uncertain, state your uncertainty clearly.");
                systemContent.AppendLine("3. CONCISE & STRUCTURED: Use clean GitHub markdown formatting, bullet points, and code blocks.");

                if (triggeredLore.Count > 0)
                {
                    systemContent.AppendLine("\n[SPECIALIZED DOMAIN KNOWLEDGE & TECHNICAL SKILLS]:");
                    foreach (var lore in triggeredLore)
                    {
                        systemContent.AppendLine($"### Reference Skill: {lore.Title}\n{lore.Content.Trim()}\n");
                    }
                }
            }
            else
            {
                // Story / Roleplay Mode Perspective Steering
                string povFocus = string.IsNullOrWhiteSpace(session.PovCharacter) ? "Omniscient Narrator" : session.PovCharacter;
                if (session.Perspective.Contains("First Person", StringComparison.OrdinalIgnoreCase))
                {
                    systemContent.AppendLine($"\n[Perspective Directive: First-Person (\"I/Me\"). Write entirely through the sensory lens and raw internal monologue of {povFocus}.]");
                }
                else if (session.Perspective.Contains("Second Person", StringComparison.OrdinalIgnoreCase))
                {
                    systemContent.AppendLine($"\n[Perspective Directive: Second-Person (\"You\"). Direct interactive reader immersion.]");
                }
                else if (!string.IsNullOrWhiteSpace(session.Perspective))
                {
                    systemContent.AppendLine($"\n[Perspective Directive: Third-Person Limited (\"He/She/They\"). Literary prose focused on the atmospheric presence of {povFocus}.]");
                }

                // Inject Active Character Dossiers
                if (triggeredChars.Count > 0)
                {
                    systemContent.AppendLine("\n[Active Character Dossiers & Personality Matrix]:");
                    foreach (var c in triggeredChars)
                    {
                        systemContent.AppendLine($"### Character: {c.Name} ({c.AgeRole})");
                        if (!string.IsNullOrWhiteSpace(c.Appearance))
                            systemContent.AppendLine($"- Physical Appearance & Scent: {c.Appearance}");
                        if (!string.IsNullOrWhiteSpace(c.Personality))
                            systemContent.AppendLine($"- Personality & Mannerisms: {c.Personality}");
                        if (!string.IsNullOrWhiteSpace(c.DialogueStyle))
                            systemContent.AppendLine($"- Dialogue Voice: {c.DialogueStyle}");
                        if (!string.IsNullOrWhiteSpace(c.KinksAndDesires))
                            systemContent.AppendLine($"- Intimate Preferences & Kinks: {c.KinksAndDesires}");
                    }
                }

                systemContent.AppendLine("\n[CHARACTER IDENTITY & RELATIONSHIP INTEGRITY - STRICT ANTI-DRIFT]:");
                systemContent.AppendLine("1. ABSOLUTE ROLE STABILITY: Never invert, swap, or confuse character identities, family titles (e.g. father vs son, parent vs child), ages, or relationship dynamics. If a character is the father/older figure, never address or treat them as the son/younger figure, and vice-versa.");
                systemContent.AppendLine("2. DIALOGUE ATTRIBUTION FIDELITY: Ensure who is speaking and who is being addressed remains 100% coherent without pronoun confusion or role switching across turns.");

                if (triggeredLore.Count > 0)
                {
                    systemContent.AppendLine("\n[Active World Information / Lorebook]:");
                    foreach (var lore in triggeredLore)
                    {
                        systemContent.AppendLine($"### {lore.Title} ({lore.Category}):\n{lore.Content.Trim()}");
                    }
                }
            }

            finalMessages.Add(new ChatMessage
            {
                Role = "system",
                Content = systemContent.ToString()
            });

            // 3. Copy isolated message history
            var messageHistory = existingMessages.Select(m => new ChatMessage
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                IsPinned = m.IsPinned
            }).ToList();

            if (!string.IsNullOrEmpty(newUserPrompt))
            {
                messageHistory.Add(new ChatMessage
                {
                    Role = "user",
                    Content = newUserPrompt
                });
            }

            // 4. Inject Author's Note at specified depth
            if (!string.IsNullOrWhiteSpace(session.AuthorNote))
            {
                int depth = Math.Clamp(session.AuthorNoteDepth, 0, messageHistory.Count);
                int targetIndex = Math.Max(0, messageHistory.Count - depth);

                var noteMessage = new ChatMessage
                {
                    Role = "system",
                    Content = $"[Author's Note: {session.AuthorNote.Trim()}]"
                };

                if (targetIndex >= messageHistory.Count)
                {
                    messageHistory.Add(noteMessage);
                }
                else
                {
                    messageHistory.Insert(targetIndex, noteMessage);
                }
            }

            finalMessages.AddRange(messageHistory);
            return finalMessages;
        }

        public async Task<List<ChatMessage>> BuildNovelInstructionMessagesAsync(
            Chapter chapter,
            Project project,
            string instructionType,
            string perspective = "Third Person (He/She)",
            string povCharacter = "Omniscient Narrator",
            string? selectedText = null,
            string? customInstruction = null)
        {
            var allCharacters = await _databaseService.GetCharacterProfilesAsync(project.Id);
            var allLore = await _databaseService.GetLoreEntriesAsync();

            string scanText = chapter.Content + " " + (selectedText ?? "");
            var triggeredChars = GetTriggeredCharacters(allCharacters.Where(c => c.IsEnabled).ToList(), scanText);
            var triggeredLore = GetTriggeredLoreEntries(allLore.Where(l => l.IsEnabled).ToList(), scanText);

            var systemContent = new StringBuilder();
            systemContent.AppendLine("You are a master fiction novelist specializing in rich, sensory-immersive, uncensored narrative prose.");
            systemContent.AppendLine($"Genre: {project.Genre}");
            systemContent.AppendLine($"Project Synopsis: {project.Synopsis}");
            systemContent.AppendLine();
            systemContent.AppendLine("CRITICAL SYSTEM DIRECTIVES:");
            systemContent.AppendLine("1. PURE STORY PROSE ONLY: Output ONLY the narrative prose. NEVER include assistant commentary, greetings, sign-offs, simulated reviews, or meta-notes (e.g. NEVER write 'Sincerely, Claude', 'End Output', 'Here is the continuation', 'Let me know if you would like me to modify').");
            systemContent.AppendLine("2. NO ROBOTIC FORMATTING: Integrate physical reactions and sensory beats seamlessly into the prose. Do not use audio tags or stage directions in brackets.");
            systemContent.AppendLine("3. PERSPECTIVE FIDELITY: Strictly adhere to the requested point of view and character voice.");

            // Perspective Steering
            if (perspective.Contains("First Person", StringComparison.OrdinalIgnoreCase))
            {
                systemContent.AppendLine($"\n[Perspective Directive: First-Person (\"I/Me\"). Write entirely through the sensory lens, physical feelings, and raw internal monologue of {povCharacter}.]");
            }
            else if (perspective.Contains("Second Person", StringComparison.OrdinalIgnoreCase))
            {
                systemContent.AppendLine($"\n[Perspective Directive: Second-Person (\"You\"). Direct reader immersion.]");
            }
            else
            {
                systemContent.AppendLine($"\n[Perspective Directive: Third-Person Limited (\"He/She\"). Atmospheric literary prose focused on {povCharacter}.]");
            }

            // Character Dossiers
            if (triggeredChars.Count > 0)
            {
                systemContent.AppendLine("\n[Active Characters in Scene]:");
                foreach (var c in triggeredChars)
                {
                    systemContent.AppendLine($"### Character: {c.Name} ({c.AgeRole})");
                    if (!string.IsNullOrWhiteSpace(c.Appearance))
                        systemContent.AppendLine($"- Physical & Scent: {c.Appearance}");
                    if (!string.IsNullOrWhiteSpace(c.Personality))
                        systemContent.AppendLine($"- Personality & Mannerisms: {c.Personality}");
                    if (!string.IsNullOrWhiteSpace(c.DialogueStyle))
                        systemContent.AppendLine($"- Voice/Dialogue: {c.DialogueStyle}");
                    if (!string.IsNullOrWhiteSpace(c.KinksAndDesires))
                        systemContent.AppendLine($"- Intimate Desires/Kinks: {c.KinksAndDesires}");
                }
            }

            if (triggeredLore.Count > 0)
            {
                systemContent.AppendLine("\n[Active Lore / Locations]:");
                foreach (var l in triggeredLore)
                {
                    systemContent.AppendLine($"- {l.Title} ({l.Category}): {l.Content}");
                }
            }

            var userContent = new StringBuilder();
            userContent.AppendLine("[Current Story Manuscript Excerpt]:");
            string contextSlice = GetRecentExcerpt(chapter.Content, 1200);
            userContent.AppendLine(string.IsNullOrWhiteSpace(contextSlice) ? "[Story Beginning]" : contextSlice);
            userContent.AppendLine();
            userContent.AppendLine("[Task Instruction]:");

            switch (instructionType.ToLowerInvariant())
            {
                case "continue":
                    userContent.AppendLine("Continue the story seamlessly from where the manuscript excerpt left off. Write the next 2-4 evocative paragraphs with vivid sensory details, character interiority, and authentic pacing. Do not summarize or jump ahead prematurely. Output only the continuing prose.");
                    break;
                case "sensory":
                    userContent.AppendLine("Take the recent scene and expand it with deep visceral sensory details (tactile touch, heat, breath, scent, physical arousal/tension, weight, subtle micro-expressions). Make the atmosphere electric and palpable. Output only the prose.");
                    break;
                case "dialogue":
                    userContent.AppendLine("Expand or continue the scene focusing on sharp, seductive, tension-laden dialogue, natural conversational banter, and expressive body language between the characters. Output only the dialogue and narrative beats.");
                    break;
                case "atmosphere":
                    userContent.AppendLine("Deepen the setting, mood, lighting, and environmental atmosphere of the current scene to create a rich, immersive emotional backdrop. Output only the prose.");
                    break;
                case "rewrite":
                    userContent.AppendLine($"Rewrite the following selected passage with richer prose, heightened emotional tension, and show-don't-tell detail:\n\"{selectedText}\"");
                    if (!string.IsNullOrWhiteSpace(customInstruction))
                    {
                        userContent.AppendLine($"Specific Guidance: {customInstruction}");
                    }
                    userContent.AppendLine("Output only the rewritten passage.");
                    break;
                case "brainstorm":
                    userContent.AppendLine("Brainstorm 4 distinct, compelling, and dramatic next plot beats or scene turns for this story, ranging from slow-burn psychological tension to high-intensity escalation. Output only the numbered ideas.");
                    break;
                default:
                    userContent.AppendLine(customInstruction ?? "Continue the narrative with high literary prose. Output only the prose.");
                    break;
            }

            return new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemContent.ToString() },
                new ChatMessage { Role = "user", Content = userContent.ToString() }
            };
        }

        public async Task<string> BuildNovelInstructionPromptAsync(
            Chapter chapter,
            Project project,
            string instructionType,
            string perspective = "Third Person (He/She)",
            string povCharacter = "Omniscient Narrator",
            string? selectedText = null,
            string? customInstruction = null)
        {
            var msgs = await BuildNovelInstructionMessagesAsync(chapter, project, instructionType, perspective, povCharacter, selectedText, customInstruction);
            return $"{msgs[0].Content}\n\nUser: {msgs[1].Content}\n\nAssistant:";
        }

        public async Task<string> BuildTwitterSmutPromptAsync(
            string mode,
            string topicOrExcerpt,
            string tone = "Sultry / Teasing")
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a viral adult fiction / spicy romance author and social media copywriter.");
            sb.AppendLine($"Target Tone: {tone}");
            sb.AppendLine("Rules: Seductive, punchy, provocative, evocative prose that stops scrolling. Never sound clinical or corporate.");

            switch (mode.ToLowerInvariant())
            {
                case "micro-smut":
                    sb.AppendLine("\n[Task]: Write a complete, standalone 1-2 tweet micro-smut drabble / sensual vignette (150-260 characters per tweet) based on the following theme or prompt:");
                    sb.AppendLine($"Theme: {topicOrExcerpt}");
                    sb.AppendLine("Focus on tactile imagery, breathless pacing, and electric tension.");
                    break;
                case "teaser":
                    sb.AppendLine("\n[Task]: Extract or craft a tantalizing, cliffhanger chapter teaser hook for X/Twitter based on the following excerpt:");
                    sb.AppendLine($"Excerpt:\n{topicOrExcerpt}");
                    sb.AppendLine("Include 1 killer quote or line, a sultry hook, and call to action, formatted under 280 characters.");
                    break;
                case "thread":
                    sb.AppendLine("\n[Task]: Write a 3 to 4 tweet mini-story thread based on this scenario:");
                    sb.AppendLine($"Scenario: {topicOrExcerpt}");
                    sb.AppendLine("Format each tweet separated by '---'. Keep each individual section under 260 characters so it fits Twitter limits with numbering.");
                    break;
                case "caption":
                    sb.AppendLine("\n[Task]: Write 3 distinct sultry, teasing, high-engagement X/Twitter captions/quotes for an author or spicy character persona based on:");
                    sb.AppendLine($"Concept: {topicOrExcerpt}");
                    break;
            }

            sb.AppendLine("\nOutput only the final tweet text, formatted clearly.");
            return await Task.FromResult(sb.ToString());
        }

        public List<CharacterProfile> GetTriggeredCharacters(List<CharacterProfile> characters, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || characters == null || characters.Count == 0)
            {
                return new List<CharacterProfile>();
            }

            var triggered = new List<CharacterProfile>();
            foreach (var c in characters)
            {
                string searchKeys = $"{c.Name}, {c.Aliases}";
                var keys = searchKeys.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(k => k.Trim())
                                     .Where(k => !string.IsNullOrEmpty(k));

                foreach (var key in keys)
                {
                    string pattern = @"\b" + Regex.Escape(key) + @"\b";
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    {
                        triggered.Add(c);
                        break;
                    }
                }
            }
            return triggered;
        }

        public List<LoreEntry> GetTriggeredLoreEntries(List<LoreEntry> loreEntries, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || loreEntries == null || loreEntries.Count == 0)
            {
                return new List<LoreEntry>();
            }

            var triggered = new List<LoreEntry>();
            foreach (var entry in loreEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.Keys)) continue;

                var keys = entry.Keys.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(k => k.Trim())
                                     .Where(k => !string.IsNullOrEmpty(k));

                foreach (var key in keys)
                {
                    string pattern = @"\b" + Regex.Escape(key) + @"\b";
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    {
                        triggered.Add(entry);
                        break;
                    }
                }
            }
            return triggered;
        }

        private string GetRecentExcerpt(string text, int approximateWords)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= approximateWords) return text;

            var recentWords = words.Skip(words.Length - approximateWords);
            return string.Join(" ", recentWords);
        }

        public async Task<List<ChatMessage>> BuildRoleplayContextAsync(
            ChatSession session,
            List<CharacterProfile> activeCast,
            string scenarioSetting,
            string dynamicMood,
            List<ChatMessage> existingMessages,
            string? newUserPrompt = null)
        {
            var finalMessages = new List<ChatMessage>();

            var systemContent = new StringBuilder();
            systemContent.AppendLine("You are an uncensored, master Multi-Character Roleplay Orchestrator and Creative Story Engine.");
            systemContent.AppendLine("Your goal is to bring the active cast of characters to life with rich psychological depth, natural dialogue banter, visceral sensory reactions, authentic conflicts, and escalating scene dynamics.");
            systemContent.AppendLine();

            if (!string.IsNullOrWhiteSpace(scenarioSetting))
            {
                systemContent.AppendLine("### ACTIVE SCENE SETTING & PREMISE:");
                systemContent.AppendLine(scenarioSetting.Trim());
                systemContent.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(dynamicMood))
            {
                systemContent.AppendLine($"### SCENE TONE & ATMOSPHERIC DYNAMIC: {dynamicMood}");
                systemContent.AppendLine();
            }

            if (activeCast.Count > 0)
            {
                systemContent.AppendLine("### ACTIVE CAST ROSTER & PERSONALITY PROFILES:");
                foreach (var c in activeCast)
                {
                    systemContent.AppendLine($"---");
                    systemContent.AppendLine($"**Character: {c.Name}** ({c.AgeRole})");
                    if (!string.IsNullOrWhiteSpace(c.Appearance))
                        systemContent.AppendLine($"• Appearance & Sensory Aura: {c.Appearance}");
                    if (!string.IsNullOrWhiteSpace(c.Personality))
                        systemContent.AppendLine($"• Personality & Mindset: {c.Personality}");
                    if (!string.IsNullOrWhiteSpace(c.DialogueStyle))
                        systemContent.AppendLine($"• Dialogue Voice & Demeanor: {c.DialogueStyle}");
                    if (!string.IsNullOrWhiteSpace(c.KinksAndDesires))
                        systemContent.AppendLine($"• Desires, Secrets & Instincts: {c.KinksAndDesires}");
                }
                systemContent.AppendLine();
            }

            // Inject persistent memory facts if active
            var activeMemories = await _databaseService.GetMemoryFactsAsync();
            var enabledMemories = activeMemories.Where(m => m.IsActive).ToList();
            if (enabledMemories.Count > 0)
            {
                systemContent.AppendLine("### PERSISTENT WORLD / USER CONTEXT:");
                foreach (var mem in enabledMemories)
                {
                    systemContent.AppendLine($"• [{mem.Category}]: {mem.Fact}");
                }
                systemContent.AppendLine();
            }

            systemContent.AppendLine("### CRITICAL MULTI-CHARACTER RP DIRECTIVES:");
            systemContent.AppendLine("1. CHARACTER FORMATTING: Clearly identify the speaking/acting character at the start of their turn using bold brackets: **[Character Name]**");
            systemContent.AppendLine("2. DYNAMIC BACK-AND-FORTH: Write authentic dialogue, body language, subtle micro-reactions (eye contact, shifting postures, physical touch, tension, voice inflections), and character interiority.");
            systemContent.AppendLine("3. SCENE CONTINUITY: Build directly off what happened in the previous lines. Advance the conversation, react to revelations, push boundaries, or introduce compelling character choices.");
            systemContent.AppendLine("4. NEVER BREAK IMMERSION: Output strictly the roleplay scene turn. Never include AI greetings, meta-commentary, or summary disclaimers.");
            systemContent.AppendLine("5. STRICT KINSHIP & ROLE INTEGRITY: Never invert, swap, or confuse character roles, familial titles (e.g. father vs son, parent vs child), ages, or relationship dynamics across turns. The father must never be addressed as 'son', and the son must never be addressed as 'father'. Maintain strict speaker and listener attribution.");

            finalMessages.Add(new ChatMessage
            {
                Role = "system",
                Content = systemContent.ToString()
            });

            // Message history
            var messageHistory = existingMessages.Select(m => new ChatMessage
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                IsPinned = m.IsPinned
            }).ToList();

            if (!string.IsNullOrEmpty(newUserPrompt))
            {
                messageHistory.Add(new ChatMessage
                {
                    Role = "user",
                    Content = newUserPrompt
                });
            }

            if (!string.IsNullOrWhiteSpace(session.AuthorNote))
            {
                int depth = Math.Clamp(session.AuthorNoteDepth, 0, messageHistory.Count);
                int targetIndex = Math.Max(0, messageHistory.Count - depth);

                var noteMessage = new ChatMessage
                {
                    Role = "system",
                    Content = $"[Director's Note: {session.AuthorNote.Trim()}]"
                };

                if (targetIndex >= messageHistory.Count)
                {
                    messageHistory.Add(noteMessage);
                }
                else
                {
                    messageHistory.Insert(targetIndex, noteMessage);
                }
            }

            finalMessages.AddRange(messageHistory);
            return finalMessages;
        }
    }
}
