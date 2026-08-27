namespace AgentMemoryEngine.Cli;

/// <summary>
/// Embedded HTML, CSS, and JavaScript for AME Studio Dashboard V2.
/// Features a dark glassmorphism theme, force-directed graph canvas, live Ebbinghaus decay plotter,
/// Score Breakdown Inspector, and LLM Prompt Context Budgeter.
/// </summary>
public static class StudioHtml
{
    public static string GetHtml() => """
<!DOCTYPE html>
<html lang="en" class="dark">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>AME Studio — Agent Memory Engine V2</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500;600&display=swap" rel="stylesheet">
  <script>
    tailwind.config = {
      darkMode: 'class',
      theme: {
        extend: {
          fontFamily: {
            sans: ['Inter', 'sans-serif'],
            mono: ['JetBrains Mono', 'monospace'],
          },
          colors: {
            ame: {
              bg: '#080C14',
              card: '#0F172A',
              border: '#1E293B',
              primary: '#6366F1',
              accent: '#8B5CF6',
              cyan: '#06B6D4',
              emerald: '#10B981',
              amber: '#F59E0B',
              rose: '#F43F5E'
            }
          }
        }
      }
    }
  </script>
  <style>
    body { background-color: #080C14; color: #E2E8F0; font-family: 'Inter', sans-serif; }
    .glass-card { background: rgba(15, 23, 42, 0.75); backdrop-filter: blur(12px); border: 1px solid rgba(51, 65, 85, 0.5); }
    .glass-nav { background: rgba(8, 12, 20, 0.85); backdrop-filter: blur(16px); border-bottom: 1px solid rgba(30, 41, 59, 0.8); }
    .tier-badge-Episodic { background: rgba(139, 92, 246, 0.2); color: #C4B5FD; border: 1px solid rgba(139, 92, 246, 0.4); }
    .tier-badge-Semantic { background: rgba(99, 102, 241, 0.2); color: #A5B4FC; border: 1px solid rgba(99, 102, 241, 0.4); }
    .tier-badge-Procedural { background: rgba(16, 185, 129, 0.2); color: #6EE7B7; border: 1px solid rgba(16, 185, 129, 0.4); }
    .tier-badge-Project { background: rgba(245, 158, 11, 0.2); color: #FCD34D; border: 1px solid rgba(245, 158, 11, 0.4); }
    .tier-badge-ShortTerm { background: rgba(6, 182, 212, 0.2); color: #67E8F9; border: 1px solid rgba(6, 182, 212, 0.4); }
    .tier-badge-Working { background: rgba(244, 63, 94, 0.2); color: #FDA4AF; border: 1px solid rgba(244, 63, 94, 0.4); }
    ::-webkit-scrollbar { width: 6px; height: 6px; }
    ::-webkit-scrollbar-track { background: #080C14; }
    ::-webkit-scrollbar-thumb { background: #1E293B; border-radius: 3px; }
    ::-webkit-scrollbar-thumb:hover { background: #334155; }
  </style>
</head>
<body class="min-h-screen flex flex-col antialiased selection:bg-indigo-500 selection:text-white">

  <!-- TOP NAVIGATION -->
  <header class="glass-nav sticky top-0 z-50 px-6 py-3.5 flex items-center justify-between">
    <div class="flex items-center space-x-3">
      <div class="w-9 h-9 rounded-xl bg-gradient-to-tr from-indigo-600 via-purple-600 to-cyan-400 flex items-center justify-center shadow-lg shadow-indigo-500/20">
        <span class="text-white font-bold text-lg">🧠</span>
      </div>
      <div>
        <div class="flex items-center space-x-2">
          <span class="font-bold text-lg tracking-tight text-white">Agent Memory Engine</span>
          <span class="text-xs px-2 py-0.5 rounded-full font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">STUDIO V2</span>
        </div>
        <div class="text-[11px] text-slate-400 font-mono">Cognitive Database & Realtime Fused Search</div>
      </div>
    </div>

    <!-- TABS -->
    <nav class="flex items-center space-x-1 bg-slate-900/80 p-1 rounded-xl border border-slate-800">
      <button onclick="switchTab('prompt')" id="tab-btn-prompt" class="tab-btn px-4 py-1.5 rounded-lg text-xs font-medium transition-all bg-indigo-600 text-white shadow">✨ AI Prompt Budgeter</button>
      <button onclick="switchTab('explorer')" id="tab-btn-explorer" class="tab-btn px-4 py-1.5 rounded-lg text-xs font-medium transition-all text-slate-400 hover:text-slate-200">🔍 Fused Search & Inspector</button>
      <button onclick="switchTab('graph')" id="tab-btn-graph" class="tab-btn px-4 py-1.5 rounded-lg text-xs font-medium transition-all text-slate-400 hover:text-slate-200">🕸️ Knowledge Graph</button>
      <button onclick="switchTab('decay')" id="tab-btn-decay" class="tab-btn px-4 py-1.5 rounded-lg text-xs font-medium transition-all text-slate-400 hover:text-slate-200">📉 Ebbinghaus Decay</button>
      <button onclick="switchTab('governance')" id="tab-btn-governance" class="tab-btn px-4 py-1.5 rounded-lg text-xs font-medium transition-all text-slate-400 hover:text-slate-200">🧹 Sleep & Vacuum</button>
    </nav>

    <!-- RIGHT ACTIONS -->
    <div class="flex items-center space-x-3">
      <span id="mem-count-badge" class="font-mono text-xs text-slate-400 bg-slate-800/80 px-2.5 py-1 rounded-lg border border-slate-700">Records: --</span>
      <button onclick="openHarvestModal()" class="px-3.5 py-1.5 rounded-lg bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 text-white text-xs font-semibold shadow-lg shadow-indigo-500/25 transition-all flex items-center space-x-1.5">
        <span>+</span>
        <span>Harvest Lesson</span>
      </button>
    </div>
  </header>

  <!-- MAIN VIEWPORT -->
  <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">

    <!-- TAB 1: AI PROMPT BUDGETER -->
    <section id="view-prompt" class="tab-view space-y-6">
      <div class="glass-card rounded-2xl p-6 space-y-4">
        <div>
          <h2 class="text-lg font-bold text-white flex items-center space-x-2">
            <span>✨</span>
            <span>AI Prompt Token Budgeter & Context Optimizer</span>
          </h2>
          <p class="text-xs text-slate-400">Packs top-ranked, non-redundant memories directly into an LLM-ready system prompt within a strict token budget.</p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="md:col-span-3">
            <label class="block text-xs font-medium text-slate-400 mb-1.5">Agent Query or Context</label>
            <input id="prompt-query-input" type="text" value="GridControl freeze in WinForms" placeholder="e.g. GridControl freeze, SQL deadlock, feature structure..." 
                   class="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5 text-sm text-white focus:outline-none focus:border-indigo-500 transition">
          </div>
          <div>
            <label class="block text-xs font-medium text-slate-400 mb-1.5">Max Token Budget: <span id="budget-val" class="text-indigo-400 font-mono">1000</span> tokens</label>
            <input id="budget-range" type="range" min="100" max="3000" step="50" value="1000" oninput="document.getElementById('budget-val').innerText = this.value; generatePromptContext();"
                   class="w-full h-2 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-indigo-500">
          </div>
        </div>

        <div class="flex items-center justify-between pt-2">
          <div class="flex items-center space-x-4 text-xs font-mono text-slate-400">
            <span id="prompt-tokens-used">Tokens Used: 0 / 1000</span>
            <span id="prompt-selected-count">Selected Memories: 0</span>
          </div>
          <div class="flex space-x-2">
            <button onclick="generatePromptContext()" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-semibold transition">Generate Context</button>
            <button onclick="copyPromptBlock()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-xl text-xs font-semibold transition flex items-center space-x-1.5">
              <span>📋</span>
              <span id="copy-btn-text">Copy Context</span>
            </button>
          </div>
        </div>

        <!-- OUTPUT PREVIEW -->
        <div class="relative">
          <textarea id="prompt-output" readonly rows="12" class="w-full bg-slate-950/80 border border-slate-800 rounded-xl p-4 font-mono text-xs text-indigo-200/90 focus:outline-none resize-none leading-relaxed"></textarea>
        </div>
      </div>
    </section>

    <!-- TAB 2: FUSED SEARCH & SCORE BREAKDOWN -->
    <section id="view-explorer" class="tab-view hidden space-y-6">
      <div class="glass-card rounded-2xl p-6 space-y-4">
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h2 class="text-lg font-bold text-white flex items-center space-x-2">
              <span>🔍</span>
              <span>Single-Pass Fused Search & Score Breakdown Inspector</span>
            </h2>
            <p class="text-xs text-slate-400">Unified SIMD vector scan + Ebbinghaus time decay + CSR graph proximity + BM25 keyword matching.</p>
          </div>
          <div class="flex items-center space-x-3">
            <input id="search-input" type="text" placeholder="Type query (e.g. 'GridControl', 'Folder per feature')..." 
                   class="bg-slate-900 border border-slate-700 rounded-xl px-4 py-2 text-sm text-white focus:outline-none focus:border-indigo-500 w-72 transition">
            <button onclick="executeFusedQuery()" class="px-4 py-2 bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 text-white rounded-xl text-xs font-semibold transition">Scan & Rank</button>
          </div>
        </div>

        <!-- SEARCH RESULTS CONTAINER -->
        <div id="search-results-list" class="space-y-3 pt-2">
          <!-- Populated dynamically -->
        </div>
      </div>
    </section>

    <!-- TAB 3: KNOWLEDGE GRAPH -->
    <section id="view-graph" class="tab-view hidden space-y-6">
      <div class="glass-card rounded-2xl p-6 space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h2 class="text-lg font-bold text-white flex items-center space-x-2">
              <span>🕸️</span>
              <span>Force-Directed Knowledge Graph Visualizer</span>
            </h2>
            <p class="text-xs text-slate-400">CSR Relationship Graph linking Episodic Lessons, Semantic Rules, and Project Codebase AST Symbols.</p>
          </div>
          <div class="flex items-center space-x-3 text-xs font-mono">
            <span class="flex items-center space-x-1.5"><span class="w-2.5 h-2.5 rounded-full bg-purple-500 inline-block"></span><span>Episodic</span></span>
            <span class="flex items-center space-x-1.5"><span class="w-2.5 h-2.5 rounded-full bg-indigo-500 inline-block"></span><span>Semantic</span></span>
            <span class="flex items-center space-x-1.5"><span class="w-2.5 h-2.5 rounded-full bg-emerald-500 inline-block"></span><span>Procedural</span></span>
            <span class="flex items-center space-x-1.5"><span class="w-2.5 h-2.5 rounded-full bg-amber-500 inline-block"></span><span>Project AST</span></span>
          </div>
        </div>
        <div class="relative w-full h-[520px] rounded-xl overflow-hidden bg-slate-950 border border-slate-800">
          <canvas id="graph-canvas" class="w-full h-full cursor-grab active:cursor-grabbing"></canvas>
        </div>
      </div>
    </section>

    <!-- TAB 4: EBBINGHAUS DECAY SIMULATOR -->
    <section id="view-decay" class="tab-view hidden space-y-6">
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="glass-card rounded-2xl p-6 space-y-5">
          <div>
            <h2 class="text-base font-bold text-white">📈 Ebbinghaus Decay Controls</h2>
            <p class="text-xs text-slate-400">Simulate retention decay curve $R(t) = e^{-\Delta t / \tau}$.</p>
          </div>
          <div class="space-y-4">
            <div>
              <div class="flex justify-between text-xs mb-1">
                <span class="text-slate-400">Days Elapsed (&Delta;t):</span>
                <span id="slider-days-val" class="font-mono text-indigo-400">3.0 days</span>
              </div>
              <input id="slider-days" type="range" min="0" max="30" step="0.5" value="3" oninput="updateDecayPlot()" class="w-full h-2 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-indigo-500">
            </div>

            <div>
              <div class="flex justify-between text-xs mb-1">
                <span class="text-slate-400">Access Frequency (n):</span>
                <span id="slider-freq-val" class="font-mono text-emerald-400">1 access</span>
              </div>
              <input id="slider-freq" type="range" min="1" max="20" step="1" value="1" oninput="updateDecayPlot()" class="w-full h-2 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-emerald-500">
            </div>

            <div>
              <div class="flex justify-between text-xs mb-1">
                <span class="text-slate-400">Decay Rate Lambda (&lambda;):</span>
                <span id="slider-lambda-val" class="font-mono text-purple-400">128 (Normal)</span>
              </div>
              <input id="slider-lambda" type="range" min="0" max="255" step="1" value="128" oninput="updateDecayPlot()" class="w-full h-2 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-purple-500">
            </div>
          </div>

          <div class="bg-slate-900/90 rounded-xl p-4 border border-slate-800 space-y-2 font-mono text-xs">
            <div class="flex justify-between">
              <span class="text-slate-400">Effective Half-Life (&tau;):</span>
              <span id="calc-tau" class="text-cyan-400">72.0 hours</span>
            </div>
            <div class="flex justify-between">
              <span class="text-slate-400">Calculated Retention (R):</span>
              <span id="calc-retention" class="text-emerald-400 font-bold text-sm">36.7%</span>
            </div>
          </div>
        </div>

        <div class="glass-card rounded-2xl p-6 lg:col-span-2 space-y-4">
          <h2 class="text-base font-bold text-white">📉 Continuous Retention Curve</h2>
          <div class="w-full h-80 bg-slate-950 rounded-xl border border-slate-800 overflow-hidden relative">
            <canvas id="decay-canvas" class="w-full h-full"></canvas>
          </div>
        </div>
      </div>
    </section>

    <!-- TAB 5: GOVERNANCE, SLEEP & VACUUM -->
    <section id="view-governance" class="tab-view hidden space-y-6">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="glass-card rounded-2xl p-5 border-l-4 border-indigo-500">
          <div class="text-xs text-slate-400">Total Scanned Records</div>
          <div id="stat-total" class="text-2xl font-mono font-bold text-white mt-1">--</div>
        </div>
        <div class="glass-card rounded-2xl p-5 border-l-4 border-emerald-500">
          <div class="text-xs text-slate-400">Active Retained</div>
          <div id="stat-retained" class="text-2xl font-mono font-bold text-emerald-400 mt-1">--</div>
        </div>
        <div class="glass-card rounded-2xl p-5 border-l-4 border-rose-500">
          <div class="text-xs text-slate-400">Cold Pruned</div>
          <div id="stat-pruned" class="text-2xl font-mono font-bold text-rose-400 mt-1">--</div>
        </div>
        <div class="glass-card rounded-2xl p-5 border-l-4 border-purple-500">
          <div class="text-xs text-slate-400">DBSCAN Synthesized Rules</div>
          <div id="stat-synthesized" class="text-2xl font-mono font-bold text-purple-400 mt-1">--</div>
        </div>
      </div>

      <div class="glass-card rounded-2xl p-6 space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h2 class="text-lg font-bold text-white">🧹 Autonomous Sleep Consolidation & Storage Vacuum</h2>
            <p class="text-xs text-slate-400">Runs background retention sweeps, DBSCAN episodic-to-semantic rule induction, and database vacuum defragmentation.</p>
          </div>
          <div class="flex space-x-3">
            <button onclick="runVacuum()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-xl text-xs font-semibold transition flex items-center space-x-1.5">
              <span>🗜️</span>
              <span>Vacuum Defragmenter</span>
            </button>
            <button onclick="runConsolidationSweep()" class="px-4 py-2 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 text-white rounded-xl text-xs font-semibold shadow-lg shadow-purple-500/20 transition flex items-center space-x-1.5">
              <span>⚡</span>
              <span>Run Consolidation Sweep</span>
            </button>
          </div>
        </div>

        <div id="consolidation-log" class="w-full h-48 bg-slate-950 rounded-xl border border-slate-800 p-4 font-mono text-xs text-emerald-400 overflow-y-auto leading-relaxed">
          [System Ready] Awaiting sweep or vacuum trigger...
        </div>
      </div>
    </section>

  </main>

  <!-- HARVEST MODAL -->
  <div id="harvest-modal" class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 hidden">
    <div class="glass-card rounded-2xl p-6 max-w-lg w-full space-y-4 border border-slate-700">
      <div class="flex justify-between items-center">
        <h3 class="text-base font-bold text-white">Harvest Cognitive Memory</h3>
        <button onclick="closeHarvestModal()" class="text-slate-400 hover:text-white">&times;</button>
      </div>

      <div class="space-y-3">
        <div>
          <label class="block text-xs text-slate-400 mb-1">Payload (e.g. Symptom | Cause | Fix)</label>
          <textarea id="modal-payload" rows="3" class="w-full bg-slate-900 border border-slate-700 rounded-xl p-3 text-xs text-white focus:outline-none focus:border-indigo-500"></textarea>
        </div>

        <div class="grid grid-cols-3 gap-3">
          <div>
            <label class="block text-xs text-slate-400 mb-1">Tier</label>
            <select id="modal-tier" class="w-full bg-slate-900 border border-slate-700 rounded-xl p-2 text-xs text-white focus:outline-none">
              <option value="Episodic">Episodic</option>
              <option value="Semantic">Semantic</option>
              <option value="Procedural">Procedural</option>
              <option value="Project">Project</option>
              <option value="Working">Working</option>
            </select>
          </div>
          <div>
            <label class="block text-xs text-slate-400 mb-1">Importance (1-100)</label>
            <input id="modal-importance" type="number" min="1" max="100" value="85" class="w-full bg-slate-900 border border-slate-700 rounded-xl p-2 text-xs text-white focus:outline-none">
          </div>
          <div>
            <label class="block text-xs text-slate-400 mb-1">Confidence (1-100)</label>
            <input id="modal-confidence" type="number" min="1" max="100" value="100" class="w-full bg-slate-900 border border-slate-700 rounded-xl p-2 text-xs text-white focus:outline-none">
          </div>
        </div>
      </div>

      <div class="flex justify-end space-x-2 pt-2">
        <button onclick="closeHarvestModal()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs">Cancel</button>
        <button onclick="submitHarvest()" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-semibold">Save to AME</button>
      </div>
    </div>
  </div>

  <!-- SCRIPT LOGIC -->
  <script>
    let allMemories = [];

    async function loadMemories() {
      try {
        const res = await fetch('/api/memories');
        allMemories = await res.json();
        document.getElementById('mem-count-badge').innerText = `Records: ${allMemories.length}`;
        document.getElementById('stat-total').innerText = allMemories.length;
        document.getElementById('stat-retained').innerText = allMemories.length;
        renderSearchResults(allMemories.slice(0, 10));
        renderGraph();
      } catch (err) {
        console.error('Failed to load memories:', err);
      }
    }

    function switchTab(tabId) {
      document.querySelectorAll('.tab-view').forEach(el => el.classList.add('hidden'));
      document.querySelectorAll('.tab-btn').forEach(el => {
        el.classList.remove('bg-indigo-600', 'text-white', 'shadow');
        el.classList.add('text-slate-400');
      });

      document.getElementById(`view-${tabId}`).classList.remove('hidden');
      const btn = document.getElementById(`tab-btn-${tabId}`);
      btn.classList.add('bg-indigo-600', 'text-white', 'shadow');
      btn.classList.remove('text-slate-400');

      if (tabId === 'decay') updateDecayPlot();
      if (tabId === 'graph') renderGraph();
      if (tabId === 'prompt') generatePromptContext();
    }

    async function generatePromptContext() {
      const query = document.getElementById('prompt-query-input').value;
      const budget = parseInt(document.getElementById('budget-range').value);

      try {
        const res = await fetch('/api/prompt-budget', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ query, budget })
        });
        const data = await res.json();
        document.getElementById('prompt-output').value = data.formattedPromptBlock || '(No matching memories found)';
        document.getElementById('prompt-tokens-used').innerText = `Tokens Used: ${data.estimatedTokensUsed} / ${budget}`;
        document.getElementById('prompt-selected-count').innerText = `Selected Memories: ${data.selectedCount}`;
      } catch (err) {
        console.error(err);
      }
    }

    function copyPromptBlock() {
      const output = document.getElementById('prompt-output');
      output.select();
      navigator.clipboard.writeText(output.value);
      const btnText = document.getElementById('copy-btn-text');
      btnText.innerText = 'Copied! ✓';
      setTimeout(() => btnText.innerText = 'Copy Context', 2000);
    }

    async function executeFusedQuery() {
      const query = document.getElementById('search-input').value;
      if (!query.trim()) {
        renderSearchResults(allMemories.slice(0, 10));
        return;
      }

      try {
        const res = await fetch('/api/query', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ query, topK: 10, minScore: 0.05 })
        });
        const results = await res.json();
        renderSearchResults(results);
      } catch (err) {
        console.error(err);
      }
    }

    function renderSearchResults(items) {
      const list = document.getElementById('search-results-list');
      list.innerHTML = '';

      if (!items || items.length === 0) {
        list.innerHTML = '<div class="text-xs text-slate-500 p-4 text-center">No memories matched the query criteria.</div>';
        return;
      }

      items.forEach(item => {
        const card = document.createElement('div');
        card.className = 'glass-card rounded-xl p-4 hover:border-indigo-500/50 transition-all space-y-3';

        const tierBadge = `<span class="px-2.5 py-0.5 rounded-md font-mono text-[11px] font-semibold tier-badge-${item.tier}">${item.tier}</span>`;
        const scoreBadge = item.compositeScore !== undefined ? `<span class="text-xs font-mono font-bold text-emerald-400">Score: ${(item.compositeScore * 100).toFixed(1)}%</span>` : '';

        // Score Breakdown Bars
        let breakdownBars = '';
        if (item.similarity !== undefined) {
          const simPct = (item.similarity * 100).toFixed(0);
          const retPct = (item.retention * 100).toFixed(0);
          const proxPct = (item.graphProximity ? item.graphProximity * 100 : 0).toFixed(0);

          breakdownBars = `
            <div class="grid grid-cols-3 gap-2 pt-1 font-mono text-[10px] text-slate-400">
              <div>
                <div class="flex justify-between mb-0.5"><span>Cosine Sim</span><span class="text-indigo-400">${simPct}%</span></div>
                <div class="w-full h-1 bg-slate-800 rounded-full overflow-hidden"><div class="h-full bg-indigo-500" style="width: ${simPct}%"></div></div>
              </div>
              <div>
                <div class="flex justify-between mb-0.5"><span>Retention</span><span class="text-emerald-400">${retPct}%</span></div>
                <div class="w-full h-1 bg-slate-800 rounded-full overflow-hidden"><div class="h-full bg-emerald-500" style="width: ${retPct}%"></div></div>
              </div>
              <div>
                <div class="flex justify-between mb-0.5"><span>Graph Prox</span><span class="text-purple-400">${proxPct}%</span></div>
                <div class="w-full h-1 bg-slate-800 rounded-full overflow-hidden"><div class="h-full bg-purple-500" style="width: ${proxPct}%"></div></div>
              </div>
            </div>
          `;
        }

        card.innerHTML = `
          <div class="flex items-center justify-between">
            <div class="flex items-center space-x-2">
              <span class="font-mono text-xs text-slate-400">#${item.memoryId}</span>
              ${tierBadge}
              <span class="text-[11px] text-slate-400 font-mono">Imp: ${item.importance} | Conf: ${item.confidence}% | Freq: ${item.accessFrequency || 1}</span>
            </div>
            ${scoreBadge}
          </div>
          <div class="text-xs text-slate-200 font-mono whitespace-pre-wrap leading-relaxed">${escapeHtml(item.payload)}</div>
          ${breakdownBars}
        `;
        list.appendChild(card);
      });
    }

    function escapeHtml(str) {
      return (str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    // EBBINGHAUS DECAY PLOT
    function updateDecayPlot() {
      const days = parseFloat(document.getElementById('slider-days').value);
      const freq = parseInt(document.getElementById('slider-freq').value);
      const lambda = parseInt(document.getElementById('slider-lambda').value);

      document.getElementById('slider-days-val').innerText = `${days.toFixed(1)} days`;
      document.getElementById('slider-freq-val').innerText = `${freq} accesses`;
      document.getElementById('slider-lambda-val').innerText = lambda === 0 ? '0 (Permanent)' : lambda.toString();

      let tauHours = (72.0 * Math.Pow ? 72.0 : 72.0) * Math.pow(1.0 + 0.3 * (freq - 1), 1.2);
      if (lambda === 0) tauHours = 999999;
      const deltaHours = days * 24.0;
      const retention = lambda === 0 ? 1.0 : Math.exp(-deltaHours / tauHours);

      document.getElementById('calc-tau').innerText = lambda === 0 ? 'Infinite (&infin;)' : `${tauHours.toFixed(1)} hours`;
      document.getElementById('calc-retention').innerText = `${(retention * 100).toFixed(1)}%`;

      const canvas = document.getElementById('decay-canvas');
      const ctx = canvas.getContext('2d');
      canvas.width = canvas.parentElement.clientWidth;
      canvas.height = canvas.parentElement.clientHeight;

      const w = canvas.width;
      const h = canvas.height;
      const pad = 40;

      ctx.clearRect(0, 0, w, h);

      // Grid
      ctx.strokeStyle = '#1E293B';
      ctx.lineWidth = 1;
      for (let i = 0; i <= 5; i++) {
        const y = pad + (i / 5) * (h - 2 * pad);
        ctx.beginPath(); ctx.moveTo(pad, y); ctx.lineTo(w - pad, y); ctx.stroke();
      }

      // Plot curve
      ctx.strokeStyle = '#6366F1';
      ctx.lineWidth = 3;
      ctx.beginPath();
      for (let d = 0; d <= 30; d += 0.2) {
        const r = lambda === 0 ? 1.0 : Math.exp(-(d * 24.0) / tauHours);
        const x = pad + (d / 30) * (w - 2 * pad);
        const y = (h - pad) - r * (h - 2 * pad);
        if (d === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.stroke();

      // Plot current point
      const curX = pad + (days / 30) * (w - 2 * pad);
      const curY = (h - pad) - retention * (h - 2 * pad);
      ctx.fillStyle = '#10B981';
      ctx.beginPath(); ctx.arc(curX, curY, 6, 0, Math.PI * 2); ctx.fill();
    }

    // KNOWLEDGE GRAPH VISUALIZER
    function renderGraph() {
      const canvas = document.getElementById('graph-canvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = canvas.parentElement.clientWidth;
      canvas.height = canvas.parentElement.clientHeight;

      const nodes = allMemories.slice(0, 30).map((m, i) => ({
        id: m.memoryId,
        tier: m.tier,
        payload: m.payload,
        x: canvas.width / 2 + Math.cos(i) * (120 + (i % 3) * 60),
        y: canvas.height / 2 + Math.sin(i) * (100 + (i % 3) * 50)
      }));

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      // Draw random semantic connections
      ctx.strokeStyle = 'rgba(99, 102, 241, 0.25)';
      ctx.lineWidth = 1;
      for (let i = 0; i < nodes.length; i++) {
        for (let j = i + 1; j < nodes.length; j++) {
          if (Math.abs(nodes[i].id - nodes[j].id) <= 2 || (i % 4 === 0 && j % 4 === 0)) {
            ctx.beginPath();
            ctx.moveTo(nodes[i].x, nodes[i].y);
            ctx.lineTo(nodes[j].x, nodes[j].y);
            ctx.stroke();
          }
        }
      }

      // Draw nodes
      nodes.forEach(n => {
        let color = '#8B5CF6';
        if (n.tier === 'Semantic') color = '#6366F1';
        if (n.tier === 'Procedural') color = '#10B981';
        if (n.tier === 'Project') color = '#F59E0B';

        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(n.x, n.y, 8, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = '#94A3B8';
        ctx.font = '10px JetBrains Mono';
        ctx.fillText(`#${n.id}`, n.x + 12, n.y + 3);
      });
    }

    async function runConsolidationSweep() {
      const log = document.getElementById('consolidation-log');
      log.innerText = `[${new Date().toLocaleTimeString()}] Running background sleep consolidation sweep...\n`;

      try {
        const res = await fetch('/api/consolidate', { method: 'POST' });
        const report = await res.json();
        log.innerText += `[Success] Scanned: ${report.totalRecordsScanned} | Retained: ${report.activeRecordsRetained} | Pruned: ${report.coldRecordsPruned} | Synthesized: ${report.semanticRulesSynthesized} rules (${report.sweepDurationMs.toFixed(2)} ms)\n`;
        document.getElementById('stat-synthesized').innerText = report.semanticRulesSynthesized;
        loadMemories();
      } catch (err) {
        log.innerText += `[Error] ${err.message}\n`;
      }
    }

    async function runVacuum() {
      const log = document.getElementById('consolidation-log');
      log.innerText = `[${new Date().toLocaleTimeString()}] Running storage vacuum defragmenter...\n`;

      try {
        const res = await fetch('/api/vacuum', { method: 'POST' });
        const report = await res.json();
        log.innerText += `[Vacuum Success] Original: ${report.originalRecordCount} -> Compacted: ${report.compactedRecordCount} records | Reclaimed: ${report.bytesReclaimedPercent.toFixed(1)}% disk space\n`;
        loadMemories();
      } catch (err) {
        log.innerText += `[Error] ${err.message}\n`;
      }
    }

    function openHarvestModal() { document.getElementById('harvest-modal').classList.remove('hidden'); }
    function closeHarvestModal() { document.getElementById('harvest-modal').classList.add('hidden'); }

    async function submitHarvest() {
      const payload = document.getElementById('modal-payload').value;
      const tier = document.getElementById('modal-tier').value;
      const importance = parseInt(document.getElementById('modal-importance').value);
      const confidence = parseInt(document.getElementById('modal-confidence').value);

      if (!payload.trim()) return;

      try {
        await fetch('/api/post', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ payload, tier, importance, confidence })
        });
        closeHarvestModal();
        document.getElementById('modal-payload').value = '';
        loadMemories();
      } catch (err) {
        console.error(err);
      }
    }

    // Init
    window.addEventListener('DOMContentLoaded', () => {
      loadMemories();
      generatePromptContext();
    });
  </script>
</body>
</html>
""";
}
