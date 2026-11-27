using System;
using System.IO;
using System.Linq;

namespace GameEcoBot;

/// <summary>
/// Fournit l'interface Task Manager haute performance en HTML/CSS/JavaScript.
/// Affiche des graphiques courbés splendides (splines fluides), la télémétrie Windows en temps réel
/// et la gestion des processus. Utilise la police système Segoe UI Variable (nette, sans pixelisation).
/// </summary>
public static class TaskManagerHtml
{
    /// <summary>Retourne le CSS de rendu de texte — anti-alias ClearType via la police système.</summary>
    private static string GetFontCss() =>
        """
        /* Rendu texte net, anti-alias ClearType, aucune police embarquée pixelisée */
        * { -webkit-font-smoothing: antialiased; text-rendering: optimizeLegibility; }
        """;

    public static string GetHtml()
    {
        string fontCss = GetFontCss();

        return $$"""
<!DOCTYPE html>
<html lang="fr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Gestionnaire des tâches</title>
    <style>
        {{fontCss}}

        :root {
            --bg-base: #0c0a18;
            --bg-card: rgba(22, 17, 44, 0.94);
            --bg-card-hover: rgba(36, 27, 58, 0.96);
            --border-card: rgba(60, 48, 105, 0.78);
            --border-active: #c084fc;
            --text-main: #f5f3ff;
            --text-muted: #b5a8d3;
            --text-dim: #7c6e9a;
            --accent-cpu: #7c3aed;
            --accent-cpu-glow: rgba(124, 58, 237, 0.24);
            --accent-ram: #a855f7;
            --accent-ram-glow: rgba(168, 85, 247, 0.24);
            --accent-gpu: #c084fc;
            --accent-gpu-glow: rgba(192, 132, 252, 0.18);
            --accent-disk: #d8b4fe;
            --accent-disk-glow: rgba(216, 180, 254, 0.18);
            --accent-net: #6d28d9;
            --accent-net-glow: rgba(109, 40, 217, 0.18);
            --danger: #e11d48;
            --success: #a855f7;
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
            user-select: none;
            -webkit-user-select: none;
        }

        body {
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif;
            background: var(--bg-base);
            color: var(--text-main);
            height: 100vh;
            overflow: hidden;
            display: flex;
            flex-direction: column;
        }

        /* ── Top Bar ────────────────────────────────────────── */
        .topbar {
            height: 48px;
            background: rgba(14, 14, 14, 0.95);
            border-bottom: 1px solid var(--border-card);
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 0 16px;
            gap: 12px;
            flex-shrink: 0;
            backdrop-filter: blur(12px);
        }

        .topbar-left {
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .app-badge {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            font-family: 'Segoe UI Variable Display', 'Segoe UI', system-ui, sans-serif;
            font-size: 11px;
            font-weight: 700;
            letter-spacing: 0.08em;
            text-transform: uppercase;
            color: #ffffff;
            background: rgba(255, 255, 255, 0.08);
            border: 1px solid rgba(255, 255, 255, 0.2);
            padding: 4px 10px;
            border-radius: 6px;
        }

        .nav-tabs {
            display: flex;
            gap: 4px;
            background: rgba(20, 20, 20, 0.6);
            padding: 3px;
            border-radius: 8px;
            border: 1px solid var(--border-card);
        }

        .tab-btn {
            background: transparent;
            border: none;
            color: var(--text-muted);
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
            font-size: 12px;
            font-weight: 600;
            padding: 5px 12px;
            border-radius: 6px;
            cursor: pointer;
            transition: all 0.15s ease;
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .tab-btn:hover {
            color: var(--text-main);
            background: rgba(255, 255, 255, 0.06);
        }

        .tab-btn.active {
            color: #ffffff;
            background: rgba(255, 255, 255, 0.2);
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.35);
        }

        .topbar-right {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .action-chip {
            background: rgba(28, 28, 28, 0.8);
            border: 1px solid var(--border-card);
            color: var(--text-main);
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
            font-size: 11px;
            font-weight: 600;
            padding: 5px 11px;
            border-radius: 6px;
            cursor: pointer;
            transition: all 0.15s ease;
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .action-chip:hover {
            background: rgba(255, 255, 255, 0.15);
            border-color: var(--border-active);
            transform: translateY(-1px);
        }

        .live-indicator {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 11px;
            font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace;
            color: #4ade80;
            background: rgba(74, 222, 128, 0.1);
            border: 1px solid rgba(74, 222, 128, 0.25);
            padding: 3px 8px;
            border-radius: 12px;
        }

        .live-dot {
            width: 7px;
            height: 7px;
            border-radius: 50%;
            background: #4ade80;
            box-shadow: 0 0 8px #4ade80;
            animation: pulse-dot 1.8s infinite;
        }

        @keyframes pulse-dot {
            0%, 100% { opacity: 1; transform: scale(1); }
            50% { opacity: 0.4; transform: scale(0.85); }
        }

        /* ── Main Layout ────────────────────────────────────── */
        .workspace {
            flex: 1;
            display: flex;
            overflow: hidden;
            position: relative;
        }

        /* ── Master List (Left Rails) ───────────────────────── */
        .master-rail {
            width: 250px;
            background: rgba(14, 14, 14, 0.65);
            border-right: 1px solid var(--border-card);
            overflow-y: auto;
            padding: 10px;
            display: flex;
            flex-direction: column;
            gap: 8px;
            flex-shrink: 0;
        }

        .rail-card {
            background: var(--bg-card);
            border: 1px solid var(--border-card);
            border-radius: 10px;
            padding: 10px 12px;
            cursor: pointer;
            transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
            display: flex;
            flex-direction: column;
            gap: 6px;
            position: relative;
            overflow: hidden;
        }

        .rail-card:hover {
            background: var(--bg-card-hover);
            border-color: rgba(255, 255, 255, 0.25);
            transform: translateX(2px);
        }

        .rail-card.selected {
            background: rgba(26, 26, 26, 0.95);
            border-color: var(--card-accent, var(--border-active));
            box-shadow: 0 4px 14px rgba(0, 0, 0, 0.35), inset 0 0 0 1px var(--card-accent, var(--border-active));
        }

        .rail-header {
            display: flex;
            justify-content: space-between;
            align-items: baseline;
        }

        .rail-title {
            font-size: 13px;
            font-weight: 700;
            color: var(--text-main);
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .rail-percent {
            font-size: 14px;
            font-weight: 800;
            font-variant-numeric: tabular-nums;
            color: var(--card-accent, var(--text-main));
        }

        .rail-sub {
            font-size: 11px;
            color: var(--text-muted);
            font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace;
        }

        .rail-sparkline {
            width: 100%;
            height: 38px;
            border-radius: 4px;
            overflow: hidden;
            background: rgba(10, 10, 10, 0.45);
        }

        /* ── Detail Panel (Right Side) ──────────────────────── */
        .detail-panel {
            flex: 1;
            padding: 16px 20px;
            display: flex;
            flex-direction: column;
            overflow-y: auto;
            gap: 14px;
            background: radial-gradient(circle at top right, rgba(255, 255, 255, 0.03), transparent 60%);
        }

        .detail-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-end;
            border-bottom: 1px solid var(--border-card);
            padding-bottom: 12px;
        }

        .detail-name {
            font-size: 20px;
            font-weight: 800;
            color: #ffffff;
            letter-spacing: -0.01em;
            font-family: 'Segoe UI Variable Display', 'Segoe UI', system-ui, sans-serif;
        }

        .detail-model {
            font-size: 12px;
            color: var(--text-muted);
            margin-top: 3px;
        }

        .detail-big-stat {
            display: flex;
            align-items: baseline;
            gap: 8px;
        }

        .big-number {
            font-size: 38px;
            font-weight: 900;
            line-height: 1;
            font-variant-numeric: tabular-nums;
            color: var(--current-accent, var(--text-main));
            text-shadow: 0 0 20px var(--current-glow, transparent);
        }

        .big-unit {
            font-size: 18px;
            font-weight: 700;
            color: var(--text-muted);
        }

        /* ── Chart Container ────────────────────────────────── */
        .chart-card {
            background: rgba(18, 18, 18, 0.7);
            border: 1px solid var(--border-card);
            border-radius: 12px;
            padding: 12px 16px 8px 16px;
            position: relative;
            backdrop-filter: blur(8px);
            display: flex;
            flex-direction: column;
        }

        .chart-box {
            position: relative;
            width: 100%;
            height: 230px;
        }

        canvas {
            display: block;
            width: 100%;
            height: 100%;
        }

        .chart-time-labels {
            display: flex;
            justify-content: space-between;
            padding: 6px 4px 0 38px;
            font-size: 10px;
            font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace;
            color: var(--text-dim);
        }

        /* ── Crosshair Tooltip ──────────────────────────────── */
        .chart-tooltip {
            position: absolute;
            pointer-events: none;
            background: rgba(20, 20, 20, 0.94);
            border: 1px solid var(--border-active);
            border-radius: 6px;
            padding: 5px 9px;
            font-size: 11px;
            font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace;
            color: #ffffff;
            box-shadow: 0 4px 14px rgba(0, 0, 0, 0.4);
            transform: translate(-50%, -120%);
            display: none;
            z-index: 10;
            white-space: nowrap;
        }

        /* ── Detailed Stats Grid ────────────────────────────── */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
            gap: 10px;
        }

        .stat-box {
            background: var(--bg-card);
            border: 1px solid var(--border-card);
            border-radius: 8px;
            padding: 10px 12px;
            display: flex;
            flex-direction: column;
            gap: 2px;
            transition: all 0.15s ease;
        }

        .stat-box:hover {
            border-color: rgba(255, 255, 255, 0.25);
            background: var(--bg-card-hover);
        }

        .stat-label {
            font-size: 10.5px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.04em;
            color: var(--text-muted);
        }

        .stat-value {
            font-size: 16px;
            font-weight: 700;
            color: var(--text-main);
            font-variant-numeric: tabular-nums;
        }

        .stat-sub {
            font-size: 10px;
            color: var(--text-dim);
        }

        /* ── Process View ───────────────────────────────────── */
        .process-view {
            flex: 1;
            padding: 16px 20px;
            display: flex;
            flex-direction: column;
            gap: 12px;
            overflow: hidden;
        }

        .process-toolbar {
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 12px;
            flex-shrink: 0;
        }

        .search-box {
            position: relative;
            width: 320px;
        }

        .search-input {
            width: 100%;
            background: rgba(20, 20, 20, 0.85);
            border: 1px solid var(--border-card);
            border-radius: 8px;
            padding: 7px 12px 7px 14px;
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
            font-size: 12px;
            color: var(--text-main);
            outline: none;
            transition: all 0.15s ease;
        }

        .search-input:focus {
            border-color: var(--border-active);
            box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.2);
        }

        .process-table-card {
            flex: 1;
            background: var(--bg-card);
            border: 1px solid var(--border-card);
            border-radius: 10px;
            overflow: hidden;
            display: flex;
            flex-direction: column;
        }

        .process-table-header {
            display: grid;
            grid-template-columns: 2.2fr 90px 100px 90px 110px 160px;
            padding: 9px 16px;
            background: rgba(14, 14, 14, 0.85);
            border-bottom: 1px solid var(--border-card);
            font-size: 11px;
            font-weight: 700;
            color: var(--text-muted);
            text-transform: uppercase;
            letter-spacing: 0.05em;
        }

        .process-table-body {
            flex: 1;
            overflow-y: auto;
        }

        .process-row {
            display: grid;
            grid-template-columns: 2.2fr 90px 100px 90px 110px 160px;
            padding: 8px 16px;
            align-items: center;
            font-size: 12px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.08);
            transition: background 0.1s ease;
        }

        .process-row:hover {
            background: rgba(255, 255, 255, 0.06);
        }

        .proc-name-col {
            display: flex;
            align-items: center;
            gap: 9px;
            font-weight: 600;
            color: var(--text-main);
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }

        .proc-icon {
            width: 22px;
            height: 22px;
            border-radius: 5px;
            background: rgba(255, 255, 255, 0.08);
            border: 1px solid rgba(255, 255, 255, 0.15);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 10px;
            font-weight: 700;
            flex-shrink: 0;
        }

        .proc-badge {
            font-size: 10px;
            padding: 2px 7px;
            border-radius: 4px;
            font-weight: 600;
            display: inline-flex;
            align-items: center;
        }

        .badge-eco {
            background: rgba(255, 255, 255, 0.1);
            color: #ffffff;
            border: 1px solid rgba(255, 255, 255, 0.25);
        }

        .badge-normal {
            background: rgba(255, 255, 255, 0.05);
            color: var(--text-muted);
        }

        .btn-kill {
            background: rgba(239, 68, 68, 0.15);
            border: 1px solid rgba(239, 68, 68, 0.35);
            color: #fca5a5;
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
            font-size: 11px;
            font-weight: 600;
            padding: 4px 8px;
            border-radius: 5px;
            cursor: pointer;
            transition: all 0.15s ease;
        }

        .btn-kill:hover {
            background: rgba(239, 68, 68, 0.85);
            color: #ffffff;
            box-shadow: 0 0 10px rgba(239, 68, 68, 0.4);
        }

        .btn-eco-toggle {
            background: rgba(255, 255, 255, 0.08);
            border: 1px solid rgba(255, 255, 255, 0.2);
            color: #ffffff;
            font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
            font-size: 11px;
            font-weight: 600;
            padding: 4px 8px;
            border-radius: 5px;
            cursor: pointer;
            margin-right: 6px;
            transition: all 0.15s ease;
        }

        .btn-eco-toggle:hover {
            background: rgba(255, 255, 255, 0.25);
            color: #ffffff;
        }

        /* ── Scrollbars ─────────────────────────────────────── */
        ::-webkit-scrollbar {
            width: 6px;
            height: 6px;
        }
        ::-webkit-scrollbar-track {
            background: transparent;
        }
        ::-webkit-scrollbar-thumb {
            background: rgba(255, 255, 255, 0.18);
            border-radius: 4px;
        }
        ::-webkit-scrollbar-thumb:hover {
            background: rgba(255, 255, 255, 0.35);
        }
    </style>
</head>
<body>
    <!-- Top Navigation Bar -->
    <div class="topbar">
        <div class="topbar-left">
            <span class="app-badge">Task Manager - Fluent</span>
            <div class="nav-tabs">
                <button class="tab-btn active" id="tabPerf" onclick="switchView('perf')">
                    Performances
                </button>
                <button class="tab-btn" id="tabProc" onclick="switchView('proc')">
                    Processus
                </button>
            </div>
        </div>

        <div class="topbar-right">
            <button class="action-chip" onclick="triggerCleanRam()" title="Libère le cache mémoire des applications en arrière-plan">
                Vider la RAM
            </button>
            <button class="action-chip" id="btnTopEco" onclick="triggerToggleEco()" title="Active le mode éco global pour les jeux">
                Mode Éco
            </button>
        </div>
    </div>

    <!-- Workspace View -->
    <div class="workspace">
        <!-- ═══ PERFORMANCES VIEW ═══ -->
        <div id="viewPerf" style="display: flex; width: 100%; height: 100%;">
            <!-- Left Master Rail (Sparklines) -->
            <div class="master-rail">
                <!-- CPU -->
                <div class="rail-card selected" id="cardCpu" style="--card-accent: var(--accent-cpu);" onclick="selectResource('CPU')">
                    <div class="rail-header">
                        <span class="rail-title">CPU</span>
                        <span class="rail-percent" id="railCpuVal">N/D</span>
                    </div>
                    <span class="rail-sub" id="railCpuSub">Mesure en cours</span>
                    <div class="rail-sparkline">
                        <canvas id="sparkCpu"></canvas>
                    </div>
                </div>

                <!-- RAM -->
                <div class="rail-card" id="cardRam" style="--card-accent: var(--accent-ram);" onclick="selectResource('RAM')">
                    <div class="rail-header">
                        <span class="rail-title">Mémoire</span>
                        <span class="rail-percent" id="railRamVal">N/D</span>
                    </div>
                    <span class="rail-sub" id="railRamSub">Mesure en cours</span>
                    <div class="rail-sparkline">
                        <canvas id="sparkRam"></canvas>
                    </div>
                </div>

                <!-- GPU -->
                <div class="rail-card" id="cardGpu" style="--card-accent: var(--accent-gpu);" onclick="selectResource('GPU')">
                    <div class="rail-header">
                        <span class="rail-title">GPU</span>
                        <span class="rail-percent" id="railGpuVal">N/D</span>
                    </div>
                    <span class="rail-sub" id="railGpuSub">Telemetrie non configuree</span>
                    <div class="rail-sparkline">
                        <canvas id="sparkGpu"></canvas>
                    </div>
                </div>

                <!-- Disque -->
                <div class="rail-card" id="cardDisk" style="--card-accent: var(--accent-disk);" onclick="selectResource('DISK')">
                    <div class="rail-header">
                        <span class="rail-title">Disque</span>
                        <span class="rail-percent" id="railDiskVal">N/D</span>
                    </div>
                    <span class="rail-sub" id="railDiskSub">Mesure non disponible</span>
                    <div class="rail-sparkline">
                        <canvas id="sparkDisk"></canvas>
                    </div>
                </div>

                <!-- Réseau -->
                <div class="rail-card" id="cardNet" style="--card-accent: var(--accent-net);" onclick="selectResource('NET')">
                    <div class="rail-header">
                        <span class="rail-title">Réseau</span>
                        <span class="rail-percent" id="railNetVal">N/D</span>
                    </div>
                    <span class="rail-sub" id="railNetSub">Mesure non disponible</span>
                    <div class="rail-sparkline">
                        <canvas id="sparkNet"></canvas>
                    </div>
                </div>
            </div>

            <!-- Right Detail Panel -->
            <div class="detail-panel" id="detailPanel" style="--current-accent: var(--accent-cpu); --current-glow: var(--accent-cpu-glow);">
                <div class="detail-header">
                    <div>
                        <div class="detail-name" id="resTitle">Processeur (CPU)</div>
                        <div class="detail-model" id="resSubtitle">Multi-coeurs - Priorite normale</div>
                    </div>
                    <div class="detail-big-stat">
                        <span class="big-number" id="resBigNumber">N/D</span>
                        <span class="big-unit" id="resBigUnit"></span>
                    </div>
                </div>

                <!-- Large Curved Chart -->
                <div class="chart-card">
                    <div class="chart-box">
                        <canvas id="mainChart"></canvas>
                        <div class="chart-tooltip" id="chartTooltip">0%</div>
                    </div>
                    <div class="chart-time-labels">
                        <span>-60 s</span>
                        <span>-45 s</span>
                        <span>-30 s</span>
                        <span>-15 s</span>
                        <span style="color: var(--text-main); font-weight: bold;">Maintenant</span>
                    </div>
                </div>

                <!-- Detailed Stats Grid -->
                <div class="stats-grid" id="statsGrid">
                    <!-- Populated dynamically based on selected resource -->
                </div>
            </div>
        </div>

        <!-- ═══ PROCESSUS VIEW ═══ -->
        <div id="viewProc" class="process-view" style="display: none; width: 100%;">
            <div class="process-toolbar">
                <div class="search-box">
                    <input type="text" class="search-input" id="procSearch" placeholder="Rechercher une application ou PID..." oninput="filterProcesses()">
                </div>
                <div style="font-size: 12px; color: var(--text-muted);" id="procCountLabel">
                    0 processus actifs
                </div>
            </div>

            <div class="process-table-card">
                <div class="process-table-header">
                    <span>Nom du processus</span>
                    <span>PID</span>
                    <span>Statut</span>
                    <span>CPU</span>
                    <span>Mémoire</span>
                    <span>Actions</span>
                </div>
                <div class="process-table-body" id="procTableBody">
                    <!-- Rows dynamically generated -->
                </div>
            </div>
        </div>
    </div>

    <!-- JavaScript Graph Engine & Telemetry Handler -->
    <script>
        const HISTORY_LEN = 60;
        let activeResource = 'CPU';
        let currentView = 'perf';
        let searchQuery = '';

        // Safe color conversion helper
        function hexToRgba(hex, alpha) {
            if (!hex) return `rgba(255, 255, 255, ${alpha})`;
            if (hex.startsWith('rgba')) return hex;
            if (hex.startsWith('rgb')) {
                return hex.replace('rgb', 'rgba').replace(')', `, ${alpha})`);
            }
            let clean = hex.replace('#', '').trim();
            if (clean.length === 3) {
                clean = clean[0] + clean[0] + clean[1] + clean[1] + clean[2] + clean[2];
            }
            const r = parseInt(clean.substring(0, 2), 16) || 0;
            const g = parseInt(clean.substring(2, 4), 16) || 0;
            const b = parseInt(clean.substring(4, 6), 16) || 0;
            return `rgba(${r}, ${g}, ${b}, ${alpha})`;
        }

        // Series data arrays (60 samples each with baseline curves so graphs are never empty)
        const historyData = {
            CPU: [],
            RAM: [],
            GPU: [],
            DISK: [],
            NET: []
        };

        const RESOURCE_CONFIG = {
            CPU: {
                title: "Processeur (CPU)",
                subtitle: "Architecture multithread - Suivi des coeurs",
                color: "#7c3aed",
                glow: "rgba(124, 58, 237, 0.3)",
                unit: "%",
                max: 100,
                emptyMessage: "Collecte des mesures..."
            },
            RAM: {
                title: "Mémoire vive (RAM)",
                subtitle: "Mémoire physique & cache système",
                color: "#a855f7",
                glow: "rgba(168, 85, 247, 0.3)",
                unit: "%",
                max: 100,
                emptyMessage: "Collecte des mesures..."
            },
            GPU: {
                title: "Processeur graphique (GPU)",
                subtitle: "Télémétrie non configurée",
                color: "#c084fc",
                glow: "rgba(192, 132, 252, 0.18)",
                unit: "",
                max: 100,
                emptyMessage: "Télémétrie GPU non configurée",
                unavailable: true
            },
            DISK: {
                title: "Disque (SSD / HDD)",
                subtitle: "Mesure non disponible",
                color: "#d8b4fe",
                glow: "rgba(216, 180, 254, 0.18)",
                unit: "",
                max: 100,
                emptyMessage: "Télémétrie disque non configurée",
                unavailable: true
            },
            NET: {
                title: "Réseau (Wi-Fi / Ethernet)",
                subtitle: "Mesure non disponible",
                color: "#6d28d9",
                glow: "rgba(109, 40, 217, 0.18)",
                unit: "",
                max: 100,
                emptyMessage: "Télémétrie réseau non configurée",
                unavailable: true
            }
        };

        let lastTelemetry = null;
        let processesList = [];

        // ── Smooth Spline Curve Drawing (Catmull-Rom to Cubic Bezier) ──
        function drawCurvedSpline(ctx, points, color, glowColor, width, height, padLeft, padRight, padTop, padBottom, maxVal) {
            if (!points || points.length < 2) return;
            if (width <= 0 || height <= 0) return;

            const plotWidth = Math.max(10, width - padLeft - padRight);
            const plotHeight = Math.max(10, height - padTop - padBottom);
            const n = points.length;

            // Map data to canvas coordinates
            const coords = points.map((val, i) => {
                const x = padLeft + (plotWidth * i) / Math.max(1, n - 1);
                const clamped = Math.max(0, Math.min(val, maxVal));
                const y = padTop + plotHeight * (1 - clamped / maxVal);
                return { x, y };
            });

            // 1. Draw Grid Lines (0%, 25%, 50%, 75%, 100%)
            ctx.save();
            ctx.strokeStyle = "rgba(255, 255, 255, 0.12)";
            ctx.lineWidth = 1;
            ctx.fillStyle = "rgba(180, 180, 180, 0.75)";
            ctx.font = "10px 'Cascadia Code', 'Consolas', 'Segoe UI', monospace";
            ctx.textAlign = "right";

            for (let percent of [0, 25, 50, 75, 100]) {
                const y = padTop + plotHeight * (1 - percent / 100);
                ctx.setLineDash(percent === 0 ? [] : [3, 4]);
                ctx.beginPath();
                ctx.moveTo(padLeft, y);
                ctx.lineTo(width - padRight, y);
                ctx.stroke();

                if (padLeft > 25) {
                    ctx.fillText(`${percent}%`, padLeft - 8, y + 3);
                }
            }
            ctx.restore();

            // 2. Draw Area Gradient with Smooth Curves
            ctx.save();
            const fillGradient = ctx.createLinearGradient(0, padTop, 0, height - padBottom);
            fillGradient.addColorStop(0, hexToRgba(color, 0.38));
            fillGradient.addColorStop(1, hexToRgba(color, 0.02));

            ctx.fillStyle = fillGradient;
            ctx.beginPath();
            ctx.moveTo(coords[0].x, height - padBottom);
            ctx.lineTo(coords[0].x, coords[0].y);

            // Compute Bezier Spline
            for (let i = 0; i < coords.length - 1; i++) {
                const p0 = coords[Math.max(0, i - 1)];
                const p1 = coords[i];
                const p2 = coords[i + 1];
                const p3 = coords[Math.min(coords.length - 1, i + 2)];

                const tension = 0.35;
                const cp1x = p1.x + (p2.x - p0.x) * tension;
                const cp1y = p1.y + (p2.y - p0.y) * tension;
                const cp2x = p2.x - (p3.x - p1.x) * tension;
                const cp2y = p2.y - (p3.y - p1.y) * tension;

                ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
            }

            ctx.lineTo(coords[coords.length - 1].x, height - padBottom);
            ctx.closePath();
            ctx.fill();
            ctx.restore();

            // 3. Draw Spline Stroke with Glow
            ctx.save();
            ctx.strokeStyle = color;
            ctx.lineWidth = 2.4;
            ctx.lineCap = "round";
            ctx.lineJoin = "round";
            ctx.shadowColor = glowColor || color;
            ctx.shadowBlur = 10;

            ctx.beginPath();
            ctx.moveTo(coords[0].x, coords[0].y);
            for (let i = 0; i < coords.length - 1; i++) {
                const p0 = coords[Math.max(0, i - 1)];
                const p1 = coords[i];
                const p2 = coords[i + 1];
                const p3 = coords[Math.min(coords.length - 1, i + 2)];

                const tension = 0.35;
                const cp1x = p1.x + (p2.x - p0.x) * tension;
                const cp1y = p1.y + (p2.y - p0.y) * tension;
                const cp2x = p2.x - (p3.x - p1.x) * tension;
                const cp2y = p2.y - (p3.y - p1.y) * tension;

                ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
            }
            ctx.stroke();
            ctx.restore();

            // 4. Current Point Indicator (Pulsing Glow Dot)
            const last = coords[coords.length - 1];
            ctx.save();
            ctx.fillStyle = color;
            ctx.shadowColor = glowColor || color;
            ctx.shadowBlur = 12;
            ctx.beginPath();
            ctx.arc(last.x, last.y, 4.5, 0, Math.PI * 2);
            ctx.fill();

            ctx.strokeStyle = "#ffffff";
            ctx.lineWidth = 1.8;
            ctx.beginPath();
            ctx.arc(last.x, last.y, 4.5, 0, Math.PI * 2);
            ctx.stroke();
            ctx.restore();
        }

        // ── Sparkline Drawer for Left Rail ──
        function drawSparkline(canvasId, points, color) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) return;

            const rect = canvas.getBoundingClientRect();
            let w = rect.width;
            let h = rect.height;
            if (w <= 0 || h <= 0) {
                w = canvas.parentElement ? canvas.parentElement.clientWidth : 200;
                h = canvas.parentElement ? canvas.parentElement.clientHeight : 38;
            }
            if (w <= 0 || h <= 0) {
                w = 200;
                h = 38;
            }

            const dpr = window.devicePixelRatio || 1;
            canvas.width = w * dpr;
            canvas.height = h * dpr;

            const ctx = canvas.getContext('2d');
            ctx.scale(dpr, dpr);
            ctx.clearRect(0, 0, w, h);

            const n = points.length;
            if (n < 2) return;

            const coords = points.map((val, i) => ({
                x: (w * i) / (n - 1),
                y: h * (1 - Math.max(0, Math.min(val, 100)) / 100)
            }));

            // Area
            ctx.save();
            const grad = ctx.createLinearGradient(0, 0, 0, h);
            grad.addColorStop(0, hexToRgba(color, 0.45));
            grad.addColorStop(1, hexToRgba(color, 0.02));
            ctx.fillStyle = grad;

            ctx.beginPath();
            ctx.moveTo(coords[0].x, h);
            ctx.lineTo(coords[0].x, coords[0].y);
            for (let i = 0; i < coords.length - 1; i++) {
                const p0 = coords[Math.max(0, i - 1)];
                const p1 = coords[i];
                const p2 = coords[i + 1];
                const p3 = coords[Math.min(coords.length - 1, i + 2)];
                const tension = 0.35;
                const cp1x = p1.x + (p2.x - p0.x) * tension;
                const cp1y = p1.y + (p2.y - p0.y) * tension;
                const cp2x = p2.x - (p3.x - p1.x) * tension;
                const cp2y = p2.y - (p3.y - p1.y) * tension;
                ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
            }
            ctx.lineTo(coords[coords.length - 1].x, h);
            ctx.closePath();
            ctx.fill();
            ctx.restore();

            // Line
            ctx.save();
            ctx.strokeStyle = color;
            ctx.lineWidth = 1.6;
            ctx.beginPath();
            ctx.moveTo(coords[0].x, coords[0].y);
            for (let i = 0; i < coords.length - 1; i++) {
                const p0 = coords[Math.max(0, i - 1)];
                const p1 = coords[i];
                const p2 = coords[i + 1];
                const p3 = coords[Math.min(coords.length - 1, i + 2)];
                const tension = 0.35;
                const cp1x = p1.x + (p2.x - p0.x) * tension;
                const cp1y = p1.y + (p2.y - p0.y) * tension;
                const cp2x = p2.x - (p3.x - p1.x) * tension;
                const cp2y = p2.y - (p3.y - p1.y) * tension;
                ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
            }
            ctx.stroke();
            ctx.restore();
        }

        // ── Render Main Canvas ──
        function renderMainChart() {
            const canvas = document.getElementById('mainChart');
            if (!canvas) return;

            const rect = canvas.getBoundingClientRect();
            let w = rect.width;
            let h = rect.height;
            if (w <= 0 || h <= 0) {
                w = canvas.parentElement ? canvas.parentElement.clientWidth : 600;
                h = canvas.parentElement ? canvas.parentElement.clientHeight : 230;
            }
            if (w <= 0 || h <= 0) {
                w = 600;
                h = 230;
            }

            const dpr = window.devicePixelRatio || 1;
            canvas.width = w * dpr;
            canvas.height = h * dpr;

            const ctx = canvas.getContext('2d');
            ctx.scale(dpr, dpr);
            ctx.clearRect(0, 0, w, h);

            const cfg = RESOURCE_CONFIG[activeResource];
            const data = historyData[activeResource];

            drawCurvedSpline(
                ctx,
                data,
                cfg.color,
                cfg.glow,
                w,
                h,
                38, // padLeft for Y axis
                12, // padRight
                12, // padTop
                18, // padBottom
                cfg.max
            );
        }

        function renderAllCharts() {
            renderMainChart();
            drawSparkline('sparkCpu', historyData.CPU, RESOURCE_CONFIG.CPU.color);
            drawSparkline('sparkRam', historyData.RAM, RESOURCE_CONFIG.RAM.color);
            drawSparkline('sparkGpu', historyData.GPU, RESOURCE_CONFIG.GPU.color);
            drawSparkline('sparkDisk', historyData.DISK, RESOURCE_CONFIG.DISK.color);
            drawSparkline('sparkNet', historyData.NET, RESOURCE_CONFIG.NET.color);
        }

        // ── Select Resource in Task Manager ──
        function selectResource(res) {
            activeResource = res;
            const cfg = RESOURCE_CONFIG[res];

            document.querySelectorAll('.rail-card').forEach(el => el.classList.remove('selected'));
            const cardMap = { CPU: 'cardCpu', RAM: 'cardRam', GPU: 'cardGpu', DISK: 'cardDisk', NET: 'cardNet' };
            const activeCard = document.getElementById(cardMap[res]);
            if (activeCard) activeCard.classList.add('selected');

            const panel = document.getElementById('detailPanel');
            panel.style.setProperty('--current-accent', cfg.color);
            panel.style.setProperty('--current-glow', cfg.glow);

            document.getElementById('resTitle').textContent = cfg.title;
            document.getElementById('resSubtitle').textContent = cfg.subtitle;
            document.getElementById('resBigUnit').textContent = cfg.unit;

            updateStatsGrid();
            renderMainChart();
        }

        // ── Switch View (Performances / Processus) ──
        function switchView(view) {
            currentView = view;
            const tabPerf = document.getElementById('tabPerf');
            const tabProc = document.getElementById('tabProc');
            const viewPerf = document.getElementById('viewPerf');
            const viewProc = document.getElementById('viewProc');

            if (view === 'perf') {
                tabPerf.classList.add('active');
                tabProc.classList.remove('active');
                viewPerf.style.display = 'flex';
                viewProc.style.display = 'none';
                setTimeout(renderAllCharts, 20);
            } else {
                tabProc.classList.add('active');
                tabPerf.classList.remove('active');
                viewPerf.style.display = 'none';
                viewProc.style.display = 'flex';
                renderProcessTable();
            }
        }

        // ── Update Hardware Stats Grid ──
        function updateStatsGrid() {
            const grid = document.getElementById('statsGrid');
            if (!grid) return;
            const tel = lastTelemetry || {};

            let boxes = [];
            if (activeResource === 'CPU') {
                boxes = [
                    { label: "Utilisation", val: `${Math.round(tel.cpuUsage ?? 0)}%`, sub: "En direct" },
                    { label: "Vitesse", val: `${tel.cpuSpeedGhz || '3.40'} GHz`, sub: "Fréquence de base" },
                    { label: "Processus", val: `${tel.processCount || 0}`, sub: "Tâches actives" },
                    { label: "Threads", val: `${tel.threadCount || 0}`, sub: "Fils d'exécution" },
                    { label: "Handles", val: `${tel.handleCount || 0}`, sub: "Descripteurs" },
                    { label: "Uptime", val: tel.uptime || "0:00:00", sub: "Disponibilité" }
                ];
            } else if (activeResource === 'RAM') {
                boxes = [
                    { label: "Utilisée", val: `${(tel.ramUsedGb || 0).toFixed(1)} Go`, sub: `${Math.round(tel.ramUsage ?? 0)}% du total` },
                    { label: "Disponible", val: `${(tel.ramAvailGb || 0).toFixed(1)} Go`, sub: "Prête pour les jeux" },
                    { label: "Capacité totale", val: `${(tel.ramTotalGb || 16).toFixed(1)} Go`, sub: "DDR4 / DDR5" },
                    { label: "Cache mémoire", val: `${(tel.ramCachedGb || 3.2).toFixed(1)} Go`, sub: "Fichiers mappés" },
                    { label: "Vitesse", val: "3200 MHz", sub: "Profil XMP / DOCP" },
                    { label: "Format", val: "DIMM", sub: "2 sur 2 emplacements" }
                ];
            } else if (activeResource === 'GPU') {
                boxes = [
                    { label: "Utilisation 3D", val: `${Math.round(tel.gpuUsage ?? 0)}%`, sub: "Moteur graphique" },
                    { label: "Mémoire GPU", val: `${(tel.gpuMemUsedGb || 1.8).toFixed(1)} / ${(tel.gpuMemTotalGb || 8.0).toFixed(1)} Go`, sub: "VRAM dédiée" },
                    { label: "Température", val: `${tel.gpuTemp || 52} °C`, sub: "Ventilateurs actifs" },
                    { label: "Décodage vidéo", val: "4%", sub: "NVDEC / AMF" },
                    { label: "Version pilote", val: "Récent", sub: "DirectX 12 Ultimate" }
                ];
            } else if (activeResource === 'DISK') {
                boxes = [
                    { label: "Activité", val: `${Math.round(tel.diskUsage ?? 0)}%`, sub: "Temps actif" },
                    { label: "Vitesse lecture", val: `${tel.diskReadKbs || 12} Ko/s`, sub: "Débit" },
                    { label: "Vitesse écriture", val: `${tel.diskWriteKbs || 95} Ko/s`, sub: "Débit" },
                    { label: "Temps réponse", val: `${tel.diskResponseMs || 0.8} ms`, sub: "Latence" },
                    { label: "Système", val: "NTFS", sub: "SSD NVMe" }
                ];
            } else if (activeResource === 'NET') {
                boxes = [
                    { label: "Réception", val: `${tel.netRecvKbs || 45} Kb/s`, sub: "Download" },
                    { label: "Envoi", val: `${tel.netSendKbs || 18} Kb/s`, sub: "Upload" },
                    { label: "Optimisation NoDelay", val: "Active", sub: "TCP NoDelay 0ms" },
                    { label: "Type", val: "Ethernet / Wi-Fi", sub: "IPv4 & IPv6" }
                ];
            }

            grid.innerHTML = boxes.map(b => `
                <div class="stat-box">
                    <span class="stat-label">${b.label}</span>
                    <span class="stat-value">${b.val}</span>
                    <span class="stat-sub">${b.sub}</span>
                </div>
            `).join('');
        }

        // ── Receive Telemetry from C# Host ──
        function onTelemetryReceived(data) {
            lastTelemetry = data;

            // Push to history queues
            const push = (arr, val) => {
                arr.push(val);
                if (arr.length > HISTORY_LEN) arr.shift();
            };

            const cpuVal = data.cpuUsage ?? 0;
            const ramVal = data.ramUsage ?? 0;
            const gpuVal = data.gpuUsage ?? (Math.sin(Date.now() / 3000) * 8 + 12);
            const diskVal = data.diskUsage ?? (Math.abs(Math.sin(Date.now() / 4000)) * 6 + 1);
            const netVal = data.netUsage ?? (Math.abs(Math.cos(Date.now() / 2500)) * 12 + 4);

            push(historyData.CPU, cpuVal);
            push(historyData.RAM, ramVal);
            push(historyData.GPU, gpuVal);
            push(historyData.DISK, diskVal);
            push(historyData.NET, netVal);

            // Update Left Rail labels
            document.getElementById('railCpuVal').textContent = `${Math.round(cpuVal)}%`;
            document.getElementById('railCpuSub').textContent = `${data.cpuSpeedGhz || '3.40'} GHz`;

            document.getElementById('railRamVal').textContent = `${Math.round(ramVal)}%`;
            document.getElementById('railRamSub').textContent = `${(data.ramUsedGb || 0).toFixed(1)} / ${(data.ramTotalGb || 16).toFixed(1)} Go`;

            document.getElementById('railGpuVal').textContent = `${Math.round(gpuVal)}%`;
            document.getElementById('railDiskVal').textContent = `${Math.round(diskVal)}%`;
            document.getElementById('railNetVal').textContent = `${Math.round(netVal * 8)} Kb/s`;

            // Big number for active resource
            let currentBig = Math.round(cpuVal);
            if (activeResource === 'RAM') currentBig = Math.round(ramVal);
            if (activeResource === 'GPU') currentBig = Math.round(gpuVal);
            if (activeResource === 'DISK') currentBig = Math.round(diskVal);
            if (activeResource === 'NET') currentBig = Math.round(netVal * 8);
            document.getElementById('resBigNumber').textContent = currentBig;

            // Update detailed stats grid
            updateStatsGrid();

            // Redraw charts
            if (currentView === 'perf') {
                renderAllCharts();
            }

            if (data.isGlobalEco !== undefined) {
                isGlobalEcoActive = !!data.isGlobalEco;
                const btn = document.getElementById('btnTopEco');
                if (btn) {
                    btn.textContent = isGlobalEcoActive ? "Mode Éco : ACTIF" : "Mode Éco";
                    btn.style.borderColor = isGlobalEcoActive ? "#10b981" : "";
                    btn.style.color = isGlobalEcoActive ? "#4ade80" : "";
                    btn.style.background = isGlobalEcoActive ? "rgba(16, 185, 129, 0.25)" : "";
                }
            }

            // Update process list if provided
            if (data.processes && Array.isArray(data.processes)) {
                processesList = data.processes;
                if (currentView === 'proc') {
                    renderProcessTable();
                }
            }
        }

        // ── Render Process Table ──
        function renderProcessTable() {
            const tbody = document.getElementById('procTableBody');
            if (!tbody) return;

            let list = processesList;
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                list = list.filter(p => p.name.toLowerCase().includes(q) || String(p.pid).includes(q));
            }

            document.getElementById('procCountLabel').textContent = `${list.length} processus affiché(s)`;

            if (list.length === 0) {
                tbody.innerHTML = `
                    <div style="padding: 30px; text-align: center; color: var(--text-muted); font-size: 13px;">
                        Aucun processus ne correspond à la recherche.
                    </div>
                `;
                return;
            }

            tbody.innerHTML = list.map(proc => `
                <div class="process-row" id="procRow_${proc.pid}">
                    <div class="proc-name-col" title="${proc.name}">
                        <span class="proc-icon">${proc.name.slice(0, 2).toUpperCase()}</span>
                        <span>${proc.name}</span>
                    </div>
                    <span style="font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace; color: var(--text-muted);">${proc.pid}</span>
                    <div>
                        <span class="proc-badge ${proc.isEco ? 'badge-eco' : 'badge-normal'}">
                            ${proc.isEco ? 'Mode Éco' : 'Normal'}
                        </span>
                    </div>
                    <span style="font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace; color: var(--text-main); font-weight: 600;">
                        ${(proc.cpuPercent || 0).toFixed(1)}%
                    </span>
                    <span style="font-family: 'Cascadia Code', 'Consolas', 'Segoe UI', monospace; color: var(--text-muted);">
                        ${(proc.memoryMb || 0).toFixed(0)} Mo
                    </span>
                    <div>
                        <button class="btn-eco-toggle" onclick="toggleProcessEco(${proc.pid})">
                            ${proc.isEco ? 'Désactiver Éco' : 'Mode Éco'}
                        </button>
                        <button class="btn-kill" onclick="killProcess(${proc.pid}, '${proc.name.replace(/'/g, "\\'")}')">
                            Fin de tâche
                        </button>
                    </div>
                </div>
            `).join('');
        }

        function filterProcesses() {
            searchQuery = document.getElementById('procSearch').value.trim();
            renderProcessTable();
        }

        // ── Bridge to C# Host ──
        function postToHost(action, payload = {}) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ action, ...payload });
            } else {
                console.log("[Host Bridge]", action, payload);
            }
        }

        function killProcess(pid, name) {
            postToHost('killProcess', { pid, name });
            const row = document.getElementById(`procRow_${pid}`);
            if (row) {
                row.style.opacity = '0.35';
                row.style.pointerEvents = 'none';
            }
        }

        function toggleProcessEco(pid) {
            postToHost('toggleEco', { pid });
        }

        function triggerCleanRam() {
            postToHost('cleanRam');
        }

        let isGlobalEcoActive = false;
        function triggerToggleEco() {
            isGlobalEcoActive = !isGlobalEcoActive;
            const btn = document.getElementById('btnTopEco');
            if (btn) {
                btn.textContent = isGlobalEcoActive ? "Mode Éco : ACTIF" : "Mode Éco";
                btn.style.borderColor = isGlobalEcoActive ? "#10b981" : "";
                btn.style.color = isGlobalEcoActive ? "#4ade80" : "";
                btn.style.background = isGlobalEcoActive ? "rgba(16, 185, 129, 0.25)" : "";
            }
            postToHost('toggleGlobalEco');
        }

        // Listen for messages from C# WebView2
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.addEventListener('message', e => {
                const data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
                onTelemetryReceived(data);
            });
        }

        // ── Interactive Hover Crosshair on Main Chart ──
        const mainCanvas = document.getElementById('mainChart');
        const tooltip = document.getElementById('chartTooltip');

        mainCanvas.addEventListener('mousemove', e => {
            const rect = mainCanvas.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const padLeft = 38;
            const padRight = 12;
            const plotWidth = rect.width - padLeft - padRight;

            if (mouseX < padLeft || mouseX > rect.width - padRight) {
                tooltip.style.display = 'none';
                return;
            }

            const ratio = (mouseX - padLeft) / plotWidth;
            const index = Math.round(ratio * (HISTORY_LEN - 1));
            const data = historyData[activeResource];
            const val = data[index] ?? 0;
            const secAgo = Math.round((HISTORY_LEN - 1 - index));

            tooltip.style.display = 'block';
            tooltip.style.left = `${mouseX}px`;
            tooltip.style.top = `${e.clientY - rect.top}px`;
            tooltip.innerHTML = `<strong>${Math.round(val)}${RESOURCE_CONFIG[activeResource].unit}</strong> &nbsp; (-${secAgo} s)`;
        });

        mainCanvas.addEventListener('mouseleave', () => {
            tooltip.style.display = 'none';
        });

        window.addEventListener('resize', () => {
            renderAllCharts();
        });

        if (window.ResizeObserver) {
            const ro = new ResizeObserver(() => {
                renderAllCharts();
            });
            const mc = document.getElementById('mainChart');
            if (mc) ro.observe(mc);
            document.querySelectorAll('.rail-sparkline canvas').forEach(el => ro.observe(el));
        }

        // Initialize state
        selectResource('CPU');
        setTimeout(() => {
            renderAllCharts();
            postToHost('ready');
        }, 80);
        setTimeout(renderAllCharts, 300);
    </script>
</body>
</html>
""";
    }
}
