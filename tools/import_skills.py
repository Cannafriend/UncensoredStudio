import sqlite3
import uuid
import datetime

db_path = r'I:\UncensoredAI\data\studio_data.db'
con = sqlite3.connect(db_path)
cur = con.cursor()

skills = [
    {
        'title': 'Blender Python API & bmesh Architecture',
        'keys': 'blender, bpy, bmesh, mathutils, depsgraph, add-on, addon, mesh editing',
        'category': 'Technical Skill',
        'content': """# BLENDER PYTHON API & SCRIPTING STANDARD
1. PREFER DIRECT DATA API OVER OPERATORS:
   - Use bpy.data and bpy.types for datablock creation and property editing.
   - Use bmesh for procedural mesh topology (bm.verts.new, bm.faces.new, bm.to_mesh).
   - Use mathutils for Matrix, Vector, Euler, and Quaternion calculations.
   - Avoid bpy.ops in headless/automation scripts unless invoking interactive tools.
2. CONTEXT & MODE MANAGEMENT:
   - Always verify active object (bpy.context.active_object) and current mode (OBJECT, EDIT_MESH).
   - Switch modes deliberately and restore when finished.
3. EVALUATED VS ORIGINAL DATA:
   - Use bpy.context.evaluated_depsgraph_get() and obj.evaluated_get(depsgraph) when inspecting post-modifier/animated geometry.
4. SAFE CREATION PATTERN:
   import bpy
   mesh = bpy.data.meshes.new('MyMesh')
   obj = bpy.data.objects.new('MyObject', mesh)
   bpy.context.scene.collection.objects.link(obj)"""
    },
    {
        'title': 'FiveM Development & High-Performance Architecture',
        'keys': 'fivem, fxmanifest, qbcore, ox_lib, oxmysql, statebag, resmon, vector3, nui',
        'category': 'Technical Skill',
        'content': """# FIVEM DEVELOPMENT & ARCHITECTURE STANDARD
1. MODERN MANIFEST (fxmanifest.lua):
   - fx_version 'cerulean', game 'gta5', lua54 'yes'.
2. LUA 5.4 VECTOR MATH & OPTIMIZATIONS:
   - NEVER use GetDistanceBetweenCoords. Always use native vector magnitude: #(pCoords - targetCoords).
   - Localize frequent natives at file scope (local GetEntityCoords = GetEntityCoords).
   - Use compile-time joaat backtick literals `prop_speaker_03` instead of GetHashKey().
3. CLIENT TICK & RESMON OPTIMIZATION:
   - Target 0.00ms idle. Never run unconditional Wait(0) loops.
   - Use dynamic sleep scaling: Wait(1500) when distant, scale to Wait(0) only within interaction distance (<2.5m).
   - Prefer ox_target or lib.points over manual distance polling.
4. SERVER SECURITY & SQL:
   - Never trust client payloads. Use hidden 'source' header: local src = source.
   - Always use parameterized queries with OxMySQL (MySQL.query.await('SELECT * WHERE id = ?', { id })).
5. STATEBAGS:
   - Prefer Entity(e).state:set('key', val, true) with AddStateBagChangeHandler over spamming network events."""
    },
    {
        'title': 'FiveM NUI & React / CEF Bridge Standard',
        'keys': 'fivem nui, react nui, SendNUIMessage, RegisterNUICallback, cef, tokens.css, uiReady',
        'category': 'Technical Skill',
        'content': """# FIVEM NUI & REACT/CEF INTEGRATION STANDARD
1. BRIDGE CONTRACT:
   - Inbound: SendNUIMessage({ action, data }) -> filtered via useNUIEvent(action, cb).
   - Outbound: fetch('https://<resource>/<endpoint>') -> single multiplexed endpoint (nuiCallback).
   - Mandatory uiReady handshake: NUI sends uiReady once after mount; Lua gates first SendNUIMessage.
   - Invisible by default: App returns null until showNUI event arrives.
   - SetNuiFocus(show, show) on open, SetNuiFocus(false, false) on every close/ESC path.
2. CEF RENDERING QUIRKS:
   - Outer box-shadow renders broken in CEF -> strictly use filter: drop-shadow().
   - Transparent root: html, body, #root { background: transparent !important }.
   - Scaling: use CSS 'zoom' instead of 'transform: scale' to prevent raster blur.
3. VITE CONFIG FOR CEF:
   - base: './', flat output names, terser minification with drop_console: true."""
    },
    {
        'title': 'RedM RDR3 Development & Prompt Architecture',
        'keys': 'redm, rdr3, vorp, rsg, UiPrompt, CreateHoldPrompt, mounts, horse, wagon',
        'category': 'Technical Skill',
        'content': """# REDM DEVELOPMENT & ARCHITECTURE STANDARD
1. MANIFEST DIRECTIVES:
   - fx_version 'cerulean', games { 'rdr3' }, rdr3_warning 'I acknowledge...', lua54 'yes'.
2. RDR3 NATIVE PROMPTS (UiPrompt):
   - Use native UiPromptRegisterBegin / UiPromptSetControlAction / UiPromptSetHoldMode for authentic 1899 prompts with 0.00ms resmon.
3. REDM CONTROL HASHES:
   - Controls are 32-bit hex hashes: [E] = 0xCEFD9220, [G] = 0x5415BE48, [R] = 0xE30CD707, [F] = 0xDFF812F9, [SPACE] = 0xD9D0E1C0, [H] = 0x24978A28.
4. SINGLE-THREAD CPU OPTIMIZATION:
   - RedM is single-threaded CPU constrained. Use dynamic sleep (Wait(1500) -> Wait(0)).
   - Use GetGamePool('CPed') / GetGamePool('CVehicle') over 0..65535 loops.
5. FRAMEWORKS:
   - Key database records by charid (VORP) or citizenid (RSG). Use decimal currency ($2.50)."""
    },
    {
        'title': 'Vue 3 Best Practices & Composition API',
        'keys': 'vue, vue3, composition api, script setup, ref, reactive, computed, sfc, composable, pinia',
        'category': 'Technical Skill',
        'content': """# VUE 3 BEST PRACTICES & COMPOSITION API
1. STANDARD STACK: Vue 3 + Composition API + <script setup lang="ts">.
2. REACTIVITY RULES:
   - Keep source state minimal with ref/reactive. Derive all calculated state with computed().
   - Avoid destructuring reactive objects without toRefs() or toRef().
   - Use watchers for side effects only. Never mutate source state inside computed getters.
3. COMPONENT BOUNDARIES & DATA FLOW:
   - Props down, Events up (defineProps, defineEmits, defineModel).
   - Keep components small and focused. Move complex business/state logic into composables (useFeature.ts).
4. TEMPLATE SAFETY:
   - SFC section order: <script> -> <template> -> <style scoped>.
   - Never mix v-if and v-for on the same element."""
    },
    {
        'title': 'Vite 8 & Modern Frontend Tooling',
        'keys': 'vite, vite.config.ts, rolldown, oxc, hmr, ssr',
        'category': 'Technical Skill',
        'content': """# VITE 8 BUILD TOOL STANDARD
1. CONFIGURATION:
   - Always prefer TypeScript (vite.config.ts) and pure ESM.
   - Use defineConfig with resolve.alias (e.g. '@': '/src').
2. ASSET HANDLING:
   - Use import.meta.glob for eager/lazy multi-file imports.
   - Use ?raw for plain text, ?url for static asset URLs.
3. PRODUCTION BUILD:
   - Set base: './' for embedded webviews/CEF.
   - Configure build.target: 'esnext' and build.outDir: 'dist'."""
    },
    {
        'title': 'QBCore Framework Development Standard',
        'keys': 'qbcore, qb-core, PlayerData, GetPlayer, AddItem, RemoveMoney',
        'category': 'Technical Skill',
        'content': """# QBCORE FRAMEWORK STANDARD
1. CORE PRINCIPLES:
   - Always validate player: local Player = QBCore.Functions.GetPlayer(source); if not Player then return end.
   - Never trust client data: validate all money amounts and items server-side.
   - Use camelCase for Lua naming conventions. Keep variables local.
2. ECONOMY & INVENTORY:
   - Use Player.Functions.AddMoney / RemoveMoney('cash'|'bank'|'crypto', amount, reason).
   - Use Player.Functions.AddItem / RemoveItem(itemName, amount, slot, info)."""
    }
]

now = datetime.datetime.utcnow().isoformat()

for s in skills:
    cur.execute('SELECT id FROM lore_entries WHERE title = ?', (s['title'],))
    row = cur.fetchone()
    if row:
        cur.execute('UPDATE lore_entries SET keys = ?, content = ?, category = ?, is_enabled = 1, updated_at = ? WHERE id = ?',
                    (s['keys'], s['content'], s['category'], now, row[0]))
    else:
        new_id = str(uuid.uuid4())
        cur.execute('INSERT INTO lore_entries (id, session_id, title, keys, content, category, is_enabled, insertion_order, updated_at) VALUES (?, NULL, ?, ?, ?, ?, 1, 100, ?)',
                    (new_id, s['title'], s['keys'], s['content'], s['category'], now))

con.commit()
con.close()
print(f"Successfully imported {len(skills)} Agent Skills into studio_data.db!")
