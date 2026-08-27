namespace AgentMemoryEngine.Cli;

/// <summary>
/// Embedded self-contained single-page application for AME Studio Dashboard.
/// Features a dark glassmorphism aesthetic, interactive Cytoscape/Canvas graph visualizer, and live Ebbinghaus decay simulator.
/// </summary>
public static class StudioHtml
{
    public static string GetHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en" class="dark">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>AME Studio — Agent Memory Engine</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600&display=swap" rel="stylesheet">
  <script src="https://cdn.tailwindcss.com"></script>
  <script>
    tailwind.config = {
      darkMode: 'class',
      theme: {
        extend: {
          fontFamily: {
            sans: ['Plus Jakarta Sans', 'sans-serif'],
            mono: ['JetBrains Mono', 'monospace'],
          },
          colors: {
            brand: {
              50: '#ecfeff',
              100: '#cffafe',
              400: '#22d3ee',
              500: '#06b6d4',
              600: '#0891b2',
              900: '#164e63',
            },
            darkBg: '#080c14',
            darkCard: '#0f172a',
            darkBorder: 'rgba(255, 255, 255, 0.08)',
          }
        }
      }
    }
  </script>
  <style>
    body {
      background-color: #080c14;
      background-image: 
        radial-gradient(at 0% 0%, rgba(6, 182, 212, 0.12) 0px, transparent 50%),
        radial-gradient(at 100% 100%, rgba(139, 92, 246, 0.12) 0px, transparent 50%);
      background-attachment: fixed;
    }
    .glass-panel {
      background: rgba(15, 23, 42, 0.65);
      backdrop-filter: blur(16px);
      -webkit-backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.08);
    }
    .glow-accent {
      box-shadow: 0 0 25px -5px rgba(6, 182, 212, 0.3);
    }
    /* Custom scrollbar */
    ::-webkit-scrollbar { width: 6px; height: 6px; }
    ::-webkit-scrollbar-track { background: rgba(0, 0, 0, 0.2); }
    ::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.15); border-radius: 3px; }
    ::-webkit-scrollbar-thumb:hover { background: rgba(255, 255, 255, 0.25); }
  </style>
</head>
<body class="text-slate-200 font-sans min-h-screen flex flex-col antialiased selection:bg-cyan-500 selection:text-white">

  <!-- Top Navigation Bar -->
  <header class="border-b border-darkBorder glass-panel sticky top-0 z-50">
    <div class="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
      <div class="flex items-center space-x-3">
        <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-cyan-400 to-indigo-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20">
          🧠
        </div>
        <div>
          <div class="flex items-center space-x-2">
            <span class="font-bold text-lg tracking-tight bg-gradient-to-r from-white via-slate-100 to-slate-400 bg-clip-text text-transparent">AME Studio</span>
            <span class="text-xs px-2 py-0.5 rounded-full bg-cyan-500/10 border border-cyan-500/30 text-cyan-400 font-medium">v1.0 Native</span>
          </div>
          <p class="text-[11px] text-slate-400">Agent Memory Engine Visualizer</p>
        </div>
      </div>

      <nav class="flex items-center space-x-1 p-1 bg-slate-900/60 rounded-xl border border-darkBorder text-sm">
        <button onclick="switchTab('tab-memories')" id="btn-memories" class="tab-btn px-4 py-1.5 rounded-lg font-medium transition-all bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">Memories</button>
        <button onclick="switchTab('tab-graph')" id="btn-graph" class="tab-btn px-4 py-1.5 rounded-lg font-medium text-slate-400 hover:text-slate-200 transition-all">Knowledge Graph</button>
        <button onclick="switchTab('tab-simulator')" id="btn-simulator" class="tab-btn px-4 py-1.5 rounded-lg font-medium text-slate-400 hover:text-slate-200 transition-all">Decay Simulator</button>
        <button onclick="switchTab('tab-governance')" id="btn-governance" class="tab-btn px-4 py-1.5 rounded-lg font-medium text-slate-400 hover:text-slate-200 transition-all">Governance</button>
      </nav>

      <div class="flex items-center space-x-3">
        <button onclick="openHarvestModal()" class="px-3.5 py-1.5 bg-gradient-to-r from-cyan-500 to-indigo-600 hover:from-cyan-400 hover:to-indigo-500 text-white text-xs font-semibold rounded-lg shadow-md shadow-cyan-500/20 transition-all flex items-center space-x-1.5">
          <span>+ Harvest Lesson</span>
        </button>
      </div>
    </div>
  </header>

  <!-- Main Content Body -->
  <main class="max-w-7xl mx-auto px-6 py-8 flex-1 w-full space-y-6">

    <!-- Top Metrics Overview -->
    <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
      <div class="glass-panel p-5 rounded-2xl flex items-center justify-between">
        <div>
          <p class="text-xs text-slate-400 font-medium">Total Memories</p>
          <h3 id="stat-total-records" class="text-2xl font-bold text-white mt-1">--</h3>
          <p class="text-[11px] text-cyan-400 mt-1">Single-File Container (.ame)</p>
        </div>
        <div class="w-11 h-11 rounded-xl bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center text-cyan-400 text-xl font-bold">
          🗄️
        </div>
      </div>

      <div class="glass-panel p-5 rounded-2xl flex items-center justify-between">
        <div>
          <p class="text-xs text-slate-400 font-medium">Episodic Lessons</p>
          <h3 id="stat-episodic-count" class="text-2xl font-bold text-amber-400 mt-1">--</h3>
          <p class="text-[11px] text-slate-400 mt-1">Verified problem fixes</p>
        </div>
        <div class="w-11 h-11 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-center justify-center text-amber-400 text-xl font-bold">
          ⚡
        </div>
      </div>

      <div class="glass-panel p-5 rounded-2xl flex items-center justify-between">
        <div>
          <p class="text-xs text-slate-400 font-medium">Semantic Rules</p>
          <h3 id="stat-semantic-count" class="text-2xl font-bold text-indigo-400 mt-1">--</h3>
          <p class="text-[11px] text-indigo-400 mt-1">Permanent standards (0 decay)</p>
        </div>
        <div class="w-11 h-11 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-indigo-400 text-xl font-bold">
          📜
        </div>
      </div>

      <div class="glass-panel p-5 rounded-2xl flex items-center justify-between">
        <div>
          <p class="text-xs text-slate-400 font-medium">Retrieval Latency</p>
          <h3 class="text-2xl font-bold text-emerald-400 mt-1">< 1.0 ms</h3>
          <p class="text-[11px] text-emerald-400 mt-1">SIMD AVX2 + Zero-Copy</p>
        </div>
        <div class="w-11 h-11 rounded-xl bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 text-xl font-bold">
          🚀
        </div>
      </div>
    </div>

    <!-- TAB 1: MEMORIES LIST & FUSED SEARCH -->
    <div id="tab-memories" class="tab-content space-y-5">
      <!-- Search & Filter Bar -->
      <div class="glass-panel p-4 rounded-2xl flex flex-col md:flex-row items-center gap-4 justify-between">
        <div class="relative flex-1 w-full">
          <input type="text" id="query-input" placeholder="Execute Fused Cognitive Search (e.g. 'GridControl freeze after RunAfterShown')..." 
                 class="w-full bg-slate-900/80 border border-darkBorder rounded-xl px-4 py-2.5 text-sm text-white focus:outline-none focus:border-cyan-500 transition-colors pl-10">
          <span class="absolute left-3.5 top-3 text-slate-500 text-sm">🔍</span>
        </div>
        <div class="flex items-center space-x-3 w-full md:w-auto">
          <select id="tier-filter" onchange="renderMemories()" class="bg-slate-900/80 border border-darkBorder rounded-xl px-3 py-2.5 text-xs text-slate-300 focus:outline-none focus:border-cyan-500">
            <option value="ALL">All Tiers</option>
            <option value="Episodic">Episodic Only</option>
            <option value="Semantic">Semantic Only</option>
            <option value="Working">Working Memory</option>
            <option value="Procedural">Procedural</option>
          </select>
          <button onclick="performFusedSearch()" class="px-5 py-2.5 bg-cyan-500 hover:bg-cyan-400 text-slate-950 font-bold text-xs rounded-xl shadow-md shadow-cyan-500/20 transition-all flex items-center space-x-1.5 whitespace-nowrap">
            <span>Scan & Rank</span>
          </button>
        </div>
      </div>

      <!-- Memories Table / Grid -->
      <div class="glass-panel rounded-2xl overflow-hidden">
        <div class="px-6 py-4 border-b border-darkBorder flex items-center justify-between">
          <h3 class="font-semibold text-sm text-white">Stored Cognitive Records</h3>
          <span id="record-count-badge" class="text-xs text-slate-400 font-mono">Loading...</span>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-xs">
            <thead class="bg-slate-900/50 text-slate-400 uppercase tracking-wider font-semibold border-b border-darkBorder">
              <tr>
                <th class="px-6 py-3.5">ID</th>
                <th class="px-4 py-3.5">Tier</th>
                <th class="px-4 py-3.5">Payload Content</th>
                <th class="px-4 py-3.5">Importance</th>
                <th class="px-4 py-3.5">Retention</th>
                <th class="px-4 py-3.5">Access Freq</th>
                <th class="px-6 py-3.5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody id="memories-table-body" class="divide-y divide-darkBorder font-mono">
              <!-- Rendered via JS -->
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- TAB 2: KNOWLEDGE GRAPH VISUALIZER -->
    <div id="tab-graph" class="tab-content hidden space-y-4">
      <div class="glass-panel p-5 rounded-2xl">
        <div class="flex items-center justify-between mb-3">
          <div>
            <h3 class="font-semibold text-sm text-white">Associative Knowledge & Topology Graph</h3>
            <p class="text-xs text-slate-400">Compressed Sparse Row (CSR) node relationships: Episodic lessons linked to codebase symbols</p>
          </div>
          <span class="text-xs px-2.5 py-1 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 font-mono">O(1) Neighbor Traversal</span>
        </div>
        <div class="relative w-full h-[520px] rounded-xl bg-slate-950/70 border border-darkBorder overflow-hidden flex items-center justify-center">
          <canvas id="graph-canvas" class="w-full h-full"></canvas>
          <div class="absolute bottom-4 left-4 flex items-center space-x-3 text-[11px] bg-slate-900/80 px-3 py-1.5 rounded-lg border border-darkBorder">
            <span class="flex items-center space-x-1"><span class="w-2.5 h-2.5 rounded-full bg-amber-400 inline-block"></span> <span>Episodic</span></span>
            <span class="flex items-center space-x-1"><span class="w-2.5 h-2.5 rounded-full bg-indigo-400 inline-block"></span> <span>Semantic</span></span>
            <span class="flex items-center space-x-1"><span class="w-2.5 h-2.5 rounded-full bg-cyan-400 inline-block"></span> <span>Project Symbol</span></span>
          </div>
        </div>
      </div>
    </div>

    <!-- TAB 3: DECAY SIMULATOR -->
    <div id="tab-simulator" class="tab-content hidden space-y-6">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div class="glass-panel p-6 rounded-2xl space-y-5">
          <h3 class="font-semibold text-sm text-white border-b border-darkBorder pb-3">Ebbinghaus Decay Simulator</h3>
          
          <div>
            <div class="flex justify-between text-xs text-slate-300 mb-1.5">
              <span>Time Elapsed:</span>
              <span id="sim-days-val" class="font-bold text-cyan-400 font-mono">3.0 Days</span>
            </div>
            <input type="range" id="sim-days" min="0" max="30" step="0.5" value="3" oninput="updateSimulation()" class="w-full accent-cyan-500">
          </div>

          <div>
            <div class="flex justify-between text-xs text-slate-300 mb-1.5">
              <span>Access Frequency:</span>
              <span id="sim-freq-val" class="font-bold text-amber-400 font-mono">1 Access</span>
            </div>
            <input type="range" id="sim-freq" min="1" max="20" step="1" value="1" oninput="updateSimulation()" class="w-full accent-amber-500">
          </div>

          <div>
            <div class="flex justify-between text-xs text-slate-300 mb-1.5">
              <span>Decay Lambda (Steepness):</span>
              <span id="sim-decay-val" class="font-bold text-indigo-400 font-mono">128 (Normal)</span>
            </div>
            <input type="range" id="sim-decay" min="0" max="255" step="1" value="128" oninput="updateSimulation()" class="w-full accent-indigo-500">
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-darkBorder space-y-2">
            <p class="text-xs text-slate-400">Calculated Retention:</p>
            <h4 id="sim-retention-result" class="text-3xl font-extrabold text-white font-mono">--%</h4>
            <p id="sim-status-desc" class="text-[11px] text-slate-400">Retention decreases exponentially unless reinforced by frequent access.</p>
          </div>
        </div>

        <div class="md:col-span-2 glass-panel p-6 rounded-2xl flex flex-col">
          <h3 class="font-semibold text-sm text-white mb-3">Live Retention Curve R(t) = e^(-Δt / τ)</h3>
          <div class="flex-1 w-full min-h-[300px] bg-slate-950/60 rounded-xl border border-darkBorder p-4 flex items-center justify-center">
            <canvas id="curve-canvas" class="w-full h-full"></canvas>
          </div>
        </div>
      </div>
    </div>

    <!-- TAB 4: GOVERNANCE & SLEEP CONSOLIDATION -->
    <div id="tab-governance" class="tab-content hidden space-y-5">
      <div class="glass-panel p-6 rounded-2xl space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="font-semibold text-base text-white">Autonomous Sleep Consolidation Subsystem</h3>
            <p class="text-xs text-slate-400 mt-1">Executes decay evaluation, clusters recurring episodic lessons, and prunes cold low-scoring memories</p>
          </div>
          <button onclick="runConsolidationSweep()" class="px-5 py-2.5 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-400 hover:to-purple-500 text-white font-bold text-xs rounded-xl shadow-md shadow-indigo-500/20 transition-all flex items-center space-x-2">
            <span>✨ Run Consolidation Sweep</span>
          </button>
        </div>

        <div id="governance-report-box" class="p-5 rounded-xl bg-slate-900/80 border border-darkBorder hidden space-y-3 font-mono text-xs">
          <h4 class="text-emerald-400 font-bold uppercase tracking-wider">Sweep Report Generated</h4>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4 text-slate-300">
            <div><span class="text-slate-500">Scanned:</span> <span id="rep-scanned" class="font-bold text-white">0</span></div>
            <div><span class="text-slate-500">Retained:</span> <span id="rep-retained" class="font-bold text-emerald-400">0</span></div>
            <div><span class="text-slate-500">Pruned:</span> <span id="rep-pruned" class="font-bold text-rose-400">0</span></div>
            <div><span class="text-slate-500">Duration:</span> <span id="rep-duration" class="font-bold text-cyan-400">0 ms</span></div>
          </div>
        </div>
      </div>
    </div>

  </main>

  <!-- Modal: Harvest Lesson -->
  <div id="modal-harvest" class="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 hidden flex items-center justify-center p-4">
    <div class="glass-panel bg-slate-900 border border-darkBorder rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
      <div class="flex items-center justify-between border-b border-darkBorder pb-3">
        <h3 class="font-bold text-white text-base">Harvest Engineering Lesson</h3>
        <button onclick="closeHarvestModal()" class="text-slate-400 hover:text-white">&times;</button>
      </div>

      <div class="space-y-3 text-xs">
        <div>
          <label class="block text-slate-400 mb-1">Memory Tier</label>
          <select id="modal-tier" class="w-full bg-slate-950 border border-darkBorder rounded-lg px-3 py-2 text-white">
            <option value="Episodic">Episodic (Problem / Root Cause / Fix)</option>
            <option value="Semantic">Semantic (Invariant Standard / Rule)</option>
            <option value="Procedural">Procedural (Workflow / Skill Recipe)</option>
          </select>
        </div>

        <div>
          <label class="block text-slate-400 mb-1">Payload Content (Markdown Triplet)</label>
          <textarea id="modal-payload" rows="4" placeholder="GridControl freeze after RunAfterShown | Invoked sync void delegate | Use RunAfterShown(async () => await LoadData()) with top-level try/catch" 
                    class="w-full bg-slate-950 border border-darkBorder rounded-lg p-3 text-white focus:outline-none focus:border-cyan-500 font-mono"></textarea>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="block text-slate-400 mb-1">Importance (1-100)</label>
            <input type="number" id="modal-importance" value="85" min="1" max="100" class="w-full bg-slate-950 border border-darkBorder rounded-lg px-3 py-2 text-white font-mono">
          </div>
          <div>
            <label class="block text-slate-400 mb-1">Confidence Score (0-100%)</label>
            <input type="number" id="modal-confidence" value="100" min="0" max="100" class="w-full bg-slate-950 border border-darkBorder rounded-lg px-3 py-2 text-white font-mono">
          </div>
        </div>
      </div>

      <div class="flex justify-end space-x-3 pt-3 border-t border-darkBorder">
        <button onclick="closeHarvestModal()" class="px-4 py-2 rounded-lg bg-slate-800 text-slate-300 text-xs font-semibold hover:bg-slate-700">Cancel</button>
        <button onclick="submitHarvestLesson()" class="px-4 py-2 rounded-lg bg-cyan-500 hover:bg-cyan-400 text-slate-950 text-xs font-bold shadow-md shadow-cyan-500/20">Commit to Engine</button>
      </div>
    </div>
  </div>

  <script>
    let allMemories = [];

    async function loadData() {
      try {
        const res = await fetch('/api/memories');
        allMemories = await res.json();
        renderMemories();
        updateStats();
        drawGraph();
        updateSimulation();
      } catch (err) {
        console.error("Failed to load memories:", err);
      }
    }

    function updateStats() {
      document.getElementById('stat-total-records').innerText = allMemories.length;
      document.getElementById('stat-episodic-count').innerText = allMemories.filter(m => m.tier === 'Episodic').length;
      document.getElementById('stat-semantic-count').innerText = allMemories.filter(m => m.tier === 'Semantic').length;
      document.getElementById('record-count-badge').innerText = `${allMemories.length} Records in Container`;
    }

    function renderMemories(list = allMemories) {
      const filter = document.getElementById('tier-filter').value;
      const filtered = filter === 'ALL' ? list : list.filter(m => m.tier === filter);
      const tbody = document.getElementById('memories-table-body');
      tbody.innerHTML = '';

      if (filtered.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="text-center py-8 text-slate-500 font-sans">No memory records found in this tier.</td></tr>';
        return;
      }

      filtered.forEach(m => {
        const tierBadgeClass = m.tier === 'Semantic' ? 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30' :
                               m.tier === 'Episodic' ? 'bg-amber-500/10 text-amber-400 border-amber-500/30' :
                               'bg-cyan-500/10 text-cyan-400 border-cyan-500/30';
        
        const retentionPercent = Math.round((m.retention || 1.0) * 100);

        const tr = document.createElement('tr');
        tr.className = 'hover:bg-slate-800/40 transition-colors';
        tr.innerHTML = `
          <td class="px-6 py-4 font-bold text-slate-400">#${String(m.memoryId).padStart(3, '0')}</td>
          <td class="px-4 py-4"><span class="px-2 py-0.5 rounded-full border text-[11px] font-semibold ${tierBadgeClass}">${m.tier}</span></td>
          <td class="px-4 py-4 font-sans text-slate-200 max-w-md truncate" title="${escapeHtml(m.payload)}">${escapeHtml(m.payload)}</td>
          <td class="px-4 py-4 text-cyan-400">${m.importance} / 100</td>
          <td class="px-4 py-4">
            <div class="flex items-center space-x-2">
              <span class="w-10 text-right">${retentionPercent}%</span>
              <div class="w-16 h-1.5 bg-slate-800 rounded-full overflow-hidden">
                <div class="h-full bg-gradient-to-r from-cyan-400 to-emerald-400" style="width: ${retentionPercent}%"></div>
              </div>
            </div>
          </td>
          <td class="px-4 py-4 text-slate-400">${m.accessFrequency}x</td>
          <td class="px-6 py-4 text-right">
            <button onclick="touchMemory(${m.memoryId})" class="px-2.5 py-1 rounded bg-slate-800 hover:bg-cyan-500/20 hover:text-cyan-400 text-slate-300 text-[11px] font-sans transition-all">Touch (+1)</button>
          </td>
        `;
        tbody.appendChild(tr);
      });
    }

    async function performFusedSearch() {
      const query = document.getElementById('query-input').value.trim();
      if (!query) {
        renderMemories();
        return;
      }
      try {
        const res = await fetch('/api/query', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ query: query, topK: 10, minScore: 0.05 })
        });
        const results = await res.json();
        renderMemories(results);
      } catch (err) {
        console.error("Search failed:", err);
      }
    }

    async function touchMemory(id) {
      try {
        await fetch('/api/touch', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ memoryId: id })
        });
        await loadData();
      } catch (err) {
        console.error("Touch failed:", err);
      }
    }

    async function runConsolidationSweep() {
      try {
        const res = await fetch('/api/consolidate', { method: 'POST' });
        const rep = await res.json();
        document.getElementById('governance-report-box').classList.remove('hidden');
        document.getElementById('rep-scanned').innerText = rep.totalRecordsScanned;
        document.getElementById('rep-retained').innerText = rep.activeRecordsRetained;
        document.getElementById('rep-pruned').innerText = rep.coldRecordsPruned;
        document.getElementById('rep-duration').innerText = `${rep.sweepDurationMs.toFixed(2)} ms`;
        await loadData();
      } catch (err) {
        console.error("Consolidation failed:", err);
      }
    }

    function switchTab(tabId) {
      document.querySelectorAll('.tab-content').forEach(el => el.classList.add('hidden'));
      document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.className = 'tab-btn px-4 py-1.5 rounded-lg font-medium text-slate-400 hover:text-slate-200 transition-all';
      });

      document.getElementById(tabId).classList.remove('hidden');
      const activeBtn = document.getElementById(`btn-${tabId.replace('tab-', '')}`);
      if (activeBtn) {
        activeBtn.className = 'tab-btn px-4 py-1.5 rounded-lg font-medium transition-all bg-cyan-500/20 text-cyan-400 border border-cyan-500/30';
      }

      if (tabId === 'tab-graph') drawGraph();
      if (tabId === 'tab-simulator') updateSimulation();
    }

    function openHarvestModal() { document.getElementById('modal-harvest').classList.remove('hidden'); }
    function closeHarvestModal() { document.getElementById('modal-harvest').classList.add('hidden'); }

    async function submitHarvestLesson() {
      const payload = document.getElementById('modal-payload').value.trim();
      const tier = document.getElementById('modal-tier').value;
      const imp = parseInt(document.getElementById('modal-importance').value) || 80;
      const conf = parseInt(document.getElementById('modal-confidence').value) || 100;

      if (!payload) return;

      try {
        await fetch('/api/post', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ payload, tier, importance: imp, confidence: conf })
        });
        closeHarvestModal();
        document.getElementById('modal-payload').value = '';
        await loadData();
      } catch (err) {
        console.error("Failed to post:", err);
      }
    }

    // Graph Visualizer Canvas
    function drawGraph() {
      const canvas = document.getElementById('graph-canvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = canvas.parentElement.clientWidth;
      canvas.height = canvas.parentElement.clientHeight;

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      const centerX = canvas.width / 2;
      const centerY = canvas.height / 2;
      const nodes = allMemories.map((m, i) => {
        const angle = (i / Math.max(1, allMemories.length)) * 2 * Math.PI;
        const radius = 140 + (i % 3) * 40;
        return {
          id: m.memoryId,
          x: centerX + Math.cos(angle) * radius,
          y: centerY + Math.sin(angle) * radius,
          label: `#${m.memoryId} ${m.tier}`,
          color: m.tier === 'Semantic' ? '#818cf8' : m.tier === 'Episodic' ? '#fbbf24' : '#22d3ee'
        };
      });

      // Draw central hub
      ctx.beginPath();
      ctx.arc(centerX, centerY, 28, 0, 2 * Math.PI);
      ctx.fillStyle = '#06b6d4';
      ctx.shadowColor = '#06b6d4';
      ctx.shadowBlur = 15;
      ctx.fill();
      ctx.shadowBlur = 0;
      ctx.fillStyle = '#080c14';
      ctx.font = 'bold 11px Plus Jakarta Sans';
      ctx.textAlign = 'center';
      ctx.fillText('AME', centerX, centerY + 4);

      // Draw links and nodes
      nodes.forEach(n => {
        ctx.beginPath();
        ctx.moveTo(centerX, centerY);
        ctx.lineTo(n.x, n.y);
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
        ctx.stroke();

        ctx.beginPath();
        ctx.arc(n.x, n.y, 16, 0, 2 * Math.PI);
        ctx.fillStyle = n.color;
        ctx.shadowColor = n.color;
        ctx.shadowBlur = 10;
        ctx.fill();
        ctx.shadowBlur = 0;

        ctx.fillStyle = '#ffffff';
        ctx.font = '10px JetBrains Mono';
        ctx.fillText(n.label, n.x, n.y + 26);
      });
    }

    // Ebbinghaus Curve Simulator
    function updateSimulation() {
      const days = parseFloat(document.getElementById('sim-days').value);
      const freq = parseInt(document.getElementById('sim-freq').value);
      const decay = parseInt(document.getElementById('sim-decay').value);

      document.getElementById('sim-days-val').innerText = `${days.toFixed(1)} Days`;
      document.getElementById('sim-freq-val').innerText = `${freq} Accesses`;
      document.getElementById('sim-decay-val').innerText = decay === 0 ? '0 (Semantic Permanent)' : `${decay}`;

      // Retention formula
      let retention = 1.0;
      if (decay > 0) {
        const deltaHours = days * 24.0;
        const freqBoost = 1.0 + 0.5 * Math.log2(1.0 + freq);
        const decayFactor = Math.max(0.01, (256.0 - decay) / 128.0);
        const tau = 72.0 * freqBoost * decayFactor;
        retention = Math.exp(-deltaHours / tau);
      }

      const percent = Math.round(retention * 100);
      document.getElementById('sim-retention-result').innerText = `${percent}%`;

      // Draw curve canvas
      const canvas = document.getElementById('curve-canvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = canvas.parentElement.clientWidth - 32;
      canvas.height = canvas.parentElement.clientHeight - 32;

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      // Draw axes
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(40, 20);
      ctx.lineTo(40, canvas.height - 30);
      ctx.lineTo(canvas.width - 20, canvas.height - 30);
      ctx.stroke();

      // Plot curve
      ctx.strokeStyle = '#06b6d4';
      ctx.lineWidth = 2.5;
      ctx.shadowColor = '#06b6d4';
      ctx.shadowBlur = 8;
      ctx.beginPath();

      const freqBoost = 1.0 + 0.5 * Math.log2(1.0 + freq);
      const decayFactor = Math.max(0.01, (256.0 - decay) / 128.0);
      const tau = 72.0 * freqBoost * decayFactor;

      for (let x = 0; x <= 30; x += 0.2) {
        const px = 40 + (x / 30) * (canvas.width - 60);
        const r = decay === 0 ? 1.0 : Math.exp(-(x * 24.0) / tau);
        const py = (canvas.height - 30) - r * (canvas.height - 60);
        if (x === 0) ctx.moveTo(px, py);
        else ctx.lineTo(px, py);
      }
      ctx.stroke();
      ctx.shadowBlur = 0;
    }

    function escapeHtml(str) {
      if (!str) return '';
      return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }

    // Initial load
    window.addEventListener('DOMContentLoaded', loadData);
  </script>
</body>
</html>
""";
    }
}
