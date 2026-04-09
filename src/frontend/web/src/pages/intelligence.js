/**
 * intelligence.js — Intentify Intelligence Page
 * Phase 1 Final: Full filter set, SerpApi Google Trends, Eden-inspired canvas
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Constants ────────────────────────────────────────────────────────────────

const TIME_WINDOWS = [
  { value: '24h', label: 'Past 24 hours' },
  { value: '7d',  label: 'Past 7 days'   },
  { value: '30d', label: 'Past 30 days'  },
  { value: '90d', label: 'Past 90 days'  },
];

const AGE_RANGES = [
  { value: '',      label: 'All ages'    },
  { value: '13-17', label: '13 – 17'    },
  { value: '18-24', label: '18 – 24'    },
  { value: '25-34', label: '25 – 34'    },
  { value: '35-44', label: '35 – 44'    },
  { value: '45-54', label: '45 – 54'    },
  { value: '55+',   label: '55 and over' },
];

const AUDIENCE_TYPES = [
  { value: '',    label: 'All audiences'  },
  { value: 'B2C', label: 'B2C (consumers)' },
  { value: 'B2B', label: 'B2B (businesses)' },
];

const SEARCH_TYPES = [
  { value: '',         label: 'Web search (default)' },
  { value: 'images',   label: 'Image search'         },
  { value: 'news',     label: 'News search'           },
  { value: 'shopping', label: 'Shopping search'       },
  { value: 'youtube',  label: 'YouTube search'        },
];

// Google Trends category taxonomy (most useful for business owners)
const GOOGLE_CATEGORIES = [
  { value: '',    label: 'All categories' },
  { value: '632', label: 'Clothing & Fashion' },
  { value: '958', label: 'Retail & Shopping'  },
  { value: '671', label: 'Health & Wellness'  },
  { value: '396', label: 'Home & Garden'      },
  { value: '958', label: 'Food & Drink'       },
  { value: '174', label: 'Business & Finance' },
  { value: '47',  label: 'Autos & Vehicles'   },
  { value: '45',  label: 'Gaming'             },
  { value: '107', label: 'Beauty & Personal'  },
  { value: '533', label: 'Sports'             },
  { value: '67',  label: 'Travel'             },
  { value: '392', label: 'Real Estate'        },
  { value: '12',  label: 'Technology'         },
  { value: '176', label: 'Finance'            },
];

// UK/Ireland sub-regions for SerpApi geo
const SUB_REGIONS = [
  { value: '',        label: 'Whole country'      },
  { value: 'GB-NIR',  label: '🏴󠁧󠁢󠁮󠁩󠁲󠁿 Northern Ireland' },
  { value: 'GB-ENG',  label: '🏴󠁧󠁢󠁥󠁮󠁧󠁿 England'          },
  { value: 'GB-SCT',  label: '🏴󠁧󠁢󠁳󠁣󠁴󠁿 Scotland'         },
  { value: 'GB-WLS',  label: '🏴󠁧󠁢󠁷󠁬󠁳󠁿 Wales'            },
  { value: 'IE',      label: '🇮🇪 Ireland'          },
  { value: 'US-NY',   label: '🗽 New York'          },
  { value: 'US-CA',   label: '☀️ California'       },
];

const DEFAULT_FILTERS = {
  siteId: '', keyword: '', category: '', location: '',
  timeWindow: '7d', audienceType: '', ageRange: '',
  categoryId: '', searchType: '', comparisonTerms: '', subRegion: '',
};

const INTENT = {
  Transactional: { bg: '#fef3c7', border: '#fcd34d', text: '#92400e', dot: '#f59e0b' },
  Commercial:    { bg: '#dbeafe', border: '#93c5fd', text: '#1e40af', dot: '#3b82f6' },
  Informational: { bg: '#d1fae5', border: '#6ee7b7', text: '#065f46', dot: '#10b981' },
};

const SCORE_STOPS = [
  { min: 80, color: '#10b981' },
  { min: 60, color: '#3b82f6' },
  { min: 40, color: '#f59e0b' },
  { min: 0,  color: '#ef4444' },
];

// ─── Utilities ────────────────────────────────────────────────────────────────

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')       e.className = v;
    else if (k === 'style')  typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const parseCsv   = v => v.split(',').map(s => s.trim()).filter(Boolean);
const joinList   = v => Array.isArray(v) ? v.join(', ') : '';
const fmtTimeAgo = v => {
  if (!v) return '';
  const m = Math.floor((Date.now() - new Date(v)) / 60000);
  if (m < 1) return 'just now'; if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60); if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
};
const fmtDate    = v => { if (!v) return ''; const d = new Date(v); return isNaN(d) ? '' : d.toLocaleString(); };
const scoreColor = s => (SCORE_STOPS.find(c => s >= c.min) || SCORE_STOPS.at(-1)).color;
const inferIntent = t => {
  const lc = (t || '').toLowerCase();
  if (/buy|price|cost|order|hire|quote|shop|purchase|deal|cheap/.test(lc)) return 'Transactional';
  if (/best|top|review|compare|vs|alternative|recommend/.test(lc))          return 'Commercial';
  return 'Informational';
};

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_intel_v2_css')) return;
  const s = document.createElement('style');
  s.id = '_intel_v2_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.i-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:22px;width:100%;max-width:1280px;padding-bottom:60px}
.i-hero{background:linear-gradient(140deg,#0f172a 0%,#1e1b4b 55%,#0f172a 100%);border-radius:20px;padding:36px 44px;position:relative;overflow:hidden}
.i-hero::before{content:'';position:absolute;inset:0;pointer-events:none;background:radial-gradient(ellipse 70% 60% at 75% 50%,rgba(99,102,241,.22) 0%,transparent 70%)}
.i-hero-eyebrow{display:inline-flex;align-items:center;gap:7px;background:rgba(99,102,241,.18);border:1px solid rgba(99,102,241,.35);color:#a5b4fc;font-size:11px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;padding:4px 12px;border-radius:999px;margin-bottom:12px}
.i-hero-pulse{width:7px;height:7px;background:#818cf8;border-radius:50%;animation:_p 2s infinite}
@keyframes _p{0%,100%{opacity:1;transform:scale(1)}50%{opacity:.4;transform:scale(.7)}}
.i-hero-h1{font-size:28px;font-weight:700;color:#f8fafc;letter-spacing:-.03em;line-height:1.12;margin-bottom:9px}
.i-hero-h1 em{font-style:normal;background:linear-gradient(90deg,#818cf8,#c4b5fd);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.i-hero-sub{font-size:13px;color:#94a3b8;line-height:1.65;max-width:520px}
.i-hero-stats{display:flex;gap:32px;margin-top:24px;padding-top:20px;border-top:1px solid rgba(255,255,255,.07)}
.i-stat-val{font-family:'JetBrains Mono',monospace;font-size:24px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em;line-height:1}
.i-stat-lbl{font-size:10px;color:#475569;text-transform:uppercase;letter-spacing:.08em;margin-top:3px}
.i-tabs{display:flex;gap:3px;background:#f1f5f9;border-radius:12px;padding:4px;overflow-x:auto}
.i-tab{flex:1;min-width:110px;padding:8px 12px;border-radius:9px;border:none;font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:12px;font-weight:500;color:#64748b;cursor:pointer;transition:all .16s;display:flex;align-items:center;justify-content:center;gap:5px;white-space:nowrap}
.i-tab:hover{background:rgba(255,255,255,.7);color:#1e293b}
.i-tab.active{background:#fff;color:#6366f1;font-weight:700;box-shadow:0 1px 5px rgba(0,0,0,.09)}
.i-canvas{background:#fff;border:1px solid #e2e8f0;border-radius:16px;overflow:hidden}
.i-canvas-hd{display:flex;align-items:center;justify-content:space-between;padding:16px 22px;border-bottom:1px solid #f1f5f9}
.i-canvas-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.i-canvas-body{padding:20px 22px}
.i-filters{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px;align-items:end}
.i-filters-adv{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px;align-items:end;margin-top:10px;padding-top:10px;border-top:1px dashed #e2e8f0}
.i-field{display:flex;flex-direction:column;gap:4px}
.i-lbl{font-size:10px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:#94a3b8}
.i-lbl-badge{font-size:9px;background:#eef2ff;color:#6366f1;padding:1px 5px;border-radius:4px;font-weight:700;margin-left:4px}
.i-input,.i-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:7px 10px;outline:none;width:100%;transition:border .14s,box-shadow .14s}
.i-input:focus,.i-select:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.i-filter-row{display:flex;align-items:center;gap:8px;padding-top:14px;flex-wrap:wrap}
.i-adv-toggle{font-size:11px;font-weight:600;color:#6366f1;cursor:pointer;background:none;border:none;padding:2px 0;display:flex;align-items:center;gap:4px;font-family:inherit}
.i-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:8px 18px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.i-btn-primary{background:#6366f1;color:#fff}
.i-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 14px rgba(99,102,241,.28)}
.i-btn-primary:disabled{opacity:.5;cursor:not-allowed;transform:none;box-shadow:none}
.i-btn-outline{background:transparent;color:#64748b;border:1px solid #e2e8f0}
.i-btn-outline:hover:not(:disabled){background:#f8fafc;color:#1e293b}
.i-btn-sm{padding:5px 12px;font-size:11px;border-radius:7px}
.i-ai-box{background:linear-gradient(135deg,#0f172a,#1e1b4b);border-radius:14px;padding:22px 26px;position:relative;overflow:hidden}
.i-ai-box::before{content:'';position:absolute;top:-20px;right:-20px;width:140px;height:140px;background:radial-gradient(circle,rgba(139,92,246,.18) 0%,transparent 70%);pointer-events:none}
.i-ai-label{display:flex;align-items:center;gap:6px;font-size:10px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#818cf8;margin-bottom:10px}
.i-ai-text{font-size:14px;line-height:1.72;color:#e2e8f0;white-space:pre-wrap}
.i-ai-footer{margin-top:11px;font-size:10px;color:#475569;font-family:'JetBrains Mono',monospace}
.i-metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px}
.i-metric{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:16px 18px;position:relative;overflow:hidden;transition:box-shadow .18s,transform .18s}
.i-metric:hover{box-shadow:0 6px 22px rgba(0,0,0,.07);transform:translateY(-2px)}
.i-metric::before{content:'';position:absolute;top:0;left:0;right:0;height:3px;background:var(--accent,#6366f1);border-radius:12px 12px 0 0}
.i-metric-icon{width:34px;height:34px;border-radius:8px;background:var(--accent-light,#eef2ff);display:flex;align-items:center;justify-content:center;font-size:16px;margin-bottom:10px}
.i-metric-val{font-family:'JetBrains Mono',monospace;font-size:25px;font-weight:700;color:#0f172a;letter-spacing:-.02em;line-height:1}
.i-metric-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;font-weight:700;margin-top:4px}
.i-metric-sub{font-size:11px;color:#64748b;margin-top:3px}
.i-score{display:flex;align-items:center;gap:6px}
.i-score-num{font-family:'JetBrains Mono',monospace;font-size:12px;font-weight:600;color:#1e293b;min-width:26px}
.i-score-track{flex:1;height:5px;background:#e2e8f0;border-radius:999px;overflow:hidden;min-width:46px}
.i-score-fill{height:100%;border-radius:999px;transition:width .6s cubic-bezier(.34,1.56,.64,1)}
.i-rank{display:inline-flex;align-items:center;justify-content:center;width:24px;height:24px;border-radius:50%;font-family:'JetBrains Mono',monospace;font-size:10px;font-weight:700}
.i-rank-1{background:#fef3c7;color:#92400e}.i-rank-2{background:#e2e8f0;color:#475569}.i-rank-3{background:#fde8e8;color:#9f1239}.i-rank-n{background:#f8fafc;color:#94a3b8}
.i-intent{display:inline-flex;align-items:center;gap:4px;padding:2px 7px;border-radius:999px;font-size:9px;font-weight:700;letter-spacing:.04em;text-transform:uppercase}
.i-intent-dot{width:4px;height:4px;border-radius:50%}
.i-rising{display:inline-flex;align-items:center;gap:3px;background:#fef3c7;color:#92400e;border:1px solid #fcd34d;padding:2px 7px;border-radius:999px;font-size:9px;font-weight:700}
.i-table-wrap{border-radius:10px;overflow:hidden;border:1px solid #e2e8f0}
.i-table{width:100%;border-collapse:collapse;font-size:12.5px}
.i-table thead th{background:#f8fafc;padding:8px 12px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.08em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.i-table tbody td{padding:11px 12px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.i-table tbody tr:last-child td{border-bottom:none}
.i-table tbody tr:hover{background:#fafbff}
.i-topic-name{font-weight:600;color:#1e293b}
.i-topic-sub{font-size:10px;color:#94a3b8;font-family:'JetBrains Mono',monospace;margin-top:2px}
.i-opps{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}
.i-opp{border-radius:12px;padding:18px 20px;border:1px solid;transition:transform .18s,box-shadow .18s}
.i-opp:hover{transform:translateY(-2px);box-shadow:0 8px 26px rgba(0,0,0,.08)}
.i-opp-head{display:flex;align-items:flex-start;justify-content:space-between;gap:8px;margin-bottom:7px}
.i-opp-title{font-size:13px;font-weight:700;color:#0f172a;line-height:1.3}
.i-opp-score{font-family:'JetBrains Mono',monospace;font-size:20px;font-weight:700;flex-shrink:0}
.i-opp-desc{font-size:11.5px;color:#64748b;line-height:1.55;margin-bottom:10px}
.i-opp-chips{display:flex;flex-wrap:wrap;gap:4px;margin-bottom:10px}
.i-opp-chip{font-size:10px;font-weight:600;padding:2px 7px;border-radius:5px;border:1px solid}
.i-opp-action{font-size:11px;font-weight:700;display:flex;align-items:center;gap:4px}
.i-profile-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px}
.i-toggle-wrap{display:flex;align-items:center;gap:9px;padding-top:20px}
.i-toggle{position:relative;width:36px;height:19px;flex-shrink:0}
.i-toggle input{opacity:0;width:0;height:0}
.i-toggle-slider{position:absolute;inset:0;background:#e2e8f0;border-radius:999px;cursor:pointer;transition:.18s}
.i-toggle-slider::before{content:'';position:absolute;left:3px;top:3px;width:13px;height:13px;background:#fff;border-radius:50%;transition:.18s;box-shadow:0 1px 3px rgba(0,0,0,.2)}
.i-toggle input:checked+.i-toggle-slider{background:#6366f1}
.i-toggle input:checked+.i-toggle-slider::before{transform:translateX(17px)}
.i-toggle-lbl{font-size:12.5px;font-weight:500;color:#334155}
.i-pill{display:inline-flex;align-items:center;gap:4px;font-size:10.5px;font-weight:700;padding:3px 9px;border-radius:999px}
.i-pill-active{background:#d1fae5;color:#065f46}.i-pill-inactive{background:#f1f5f9;color:#64748b}.i-pill-loading{background:#dbeafe;color:#1e40af}
.i-refresh-bar{display:flex;align-items:center;justify-content:space-between;padding:9px 14px;background:#f8fafc;border-radius:9px;border:1px solid #e2e8f0}
.i-refresh-info{display:flex;align-items:center;gap:7px;font-size:11.5px;color:#64748b}
.i-live-dot{width:7px;height:7px;background:#10b981;border-radius:50%;box-shadow:0 0 0 0 rgba(16,185,129,.4);animation:_lp 2s infinite}
@keyframes _lp{0%{box-shadow:0 0 0 0 rgba(16,185,129,.4)}70%{box-shadow:0 0 0 6px rgba(16,185,129,0)}100%{box-shadow:0 0 0 0 rgba(16,185,129,0)}}
.i-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:5px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
.i-empty{text-align:center;padding:52px 24px;display:flex;flex-direction:column;align-items:center;gap:9px}
.i-empty-icon{font-size:40px;opacity:.35}
.i-empty-title{font-size:14px;font-weight:700;color:#334155}
.i-empty-desc{font-size:12px;color:#94a3b8;max-width:290px;line-height:1.6}
.i-fade{animation:_fi .3s cubic-bezier(.34,1.2,.64,1) forwards}
@keyframes _fi{from{opacity:0;transform:translateY(9px)}to{opacity:1;transform:translateY(0)}}
.i-chip{display:inline-flex;align-items:center;gap:3px;padding:2px 7px;background:#f1f5f9;border:1px solid #e2e8f0;border-radius:5px;font-size:10px;font-weight:700;color:#475569;font-family:'JetBrains Mono',monospace}
.i-onboard{background:#fff;border:2px dashed #c7d2fe;border-radius:14px;padding:32px 28px;display:flex;flex-direction:column;align-items:center;gap:12px;text-align:center}
.i-onboard-icon{font-size:40px}
.i-onboard-title{font-size:15px;font-weight:700;color:#1e293b}
.i-onboard-desc{font-size:12.5px;color:#64748b;max-width:380px;line-height:1.6}
.i-onboard-steps{display:flex;gap:20px;margin-top:4px;flex-wrap:wrap;justify-content:center}
.i-onboard-step{display:flex;flex-direction:column;align-items:center;gap:5px;width:110px;font-size:11px;color:#64748b;text-align:center}
.i-onboard-step-num{width:30px;height:30px;border-radius:50%;background:#eef2ff;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:12px;color:#6366f1}
.i-section-hd{display:flex;align-items:center;justify-content:space-between;margin-bottom:12px}
.i-section-title{font-size:12.5px;font-weight:700;color:#0f172a}
.i-section-meta{font-size:10.5px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
/* Network signals */
.i-ns-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}
@media(max-width:860px){.i-ns-grid{grid-template-columns:1fr}}
.i-ns-panel{background:#fff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden}
.i-ns-panel-hd{padding:12px 16px;border-bottom:1px solid #f1f5f9;font-size:12px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:6px}
.i-ns-panel-body{padding:12px 16px;display:flex;flex-direction:column;gap:8px}
.i-ns-bar-row{display:flex;flex-direction:column;gap:2px}
.i-ns-bar-label{font-size:11.5px;font-weight:600;color:#1e293b;display:flex;justify-content:space-between}
.i-ns-bar-track{height:6px;background:#e2e8f0;border-radius:999px;overflow:hidden}
.i-ns-bar-fill{height:100%;background:linear-gradient(90deg,#6366f1,#818cf8);border-radius:999px;transition:width .5s ease}
.i-ns-cat-pill{display:inline-flex;align-items:center;gap:4px;padding:3px 9px;border-radius:999px;font-size:10.5px;font-weight:700;cursor:default}
.i-ns-cat-hot{background:#eef2ff;color:#4338ca;border:1px solid #c7d2fe}
.i-ns-cat-norm{background:#f1f5f9;color:#475569;border:1px solid #e2e8f0}
.i-ns-country-row{display:flex;align-items:center;gap:8px;font-size:12px;color:#1e293b}
.i-ns-country-count{margin-left:auto;font-family:'JetBrains Mono',monospace;font-size:11px;color:#64748b}
.i-ns-footer{font-size:11px;color:#94a3b8;padding:10px 0 2px;border-top:1px solid #f1f5f9;margin-top:4px;line-height:1.6}
.i-ns-footer strong{color:#475569}
.i-ns-product-scroll{display:flex;gap:10px;overflow-x:auto;padding-bottom:4px}
.i-ns-product-card{min-width:150px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;flex-shrink:0}
.i-ns-product-name{font-size:12px;font-weight:700;color:#1e293b;margin-bottom:6px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.i-ns-product-badges{display:flex;gap:4px;flex-wrap:wrap}
.i-ns-product-badge{font-size:9.5px;font-weight:700;padding:2px 6px;border-radius:5px;border:1px solid}
.i-ns-filters{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:14px}
.i-ns-skel{height:32px;border-radius:8px}
/* Competitor signals */
.i-cs-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}
@media(max-width:700px){.i-cs-grid{grid-template-columns:1fr}}
.i-cs-col-hd{font-size:11.5px;font-weight:700;color:#0f172a;margin-bottom:10px;padding-bottom:8px;border-bottom:1px solid #f1f5f9}
.i-cs-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:10px 12px;margin-bottom:6px}
.i-cs-card-rising{border-left:3px solid #10b981}
.i-cs-kw{font-size:12.5px;font-weight:600;color:#1e293b;margin-bottom:5px;display:flex;align-items:center;gap:6px;flex-wrap:wrap}
.i-cs-bar-wrap{display:flex;align-items:center;gap:6px}
.i-cs-bar-track{flex:1;height:4px;background:#e2e8f0;border-radius:999px;overflow:hidden}
.i-cs-bar-fill{height:100%;border-radius:999px;transition:width .5s ease}
.i-cs-bar-val{font-size:10px;font-family:'JetBrains Mono',monospace;color:#94a3b8;min-width:24px;text-align:right}
.i-cs-intent{display:inline-flex;align-items:center;padding:1px 6px;border-radius:5px;font-size:9.5px;font-weight:700;border:1px solid;flex-shrink:0}
.i-cs-intent-t{background:#d1fae5;color:#065f46;border-color:#6ee7b7}
.i-cs-intent-c{background:#dbeafe;color:#1e40af;border-color:#93c5fd}
.i-cs-intent-i{background:#f1f5f9;color:#475569;border-color:#e2e8f0}
.i-cs-ai-box{background:#eef2ff;border-left:4px solid #6366f1;border-radius:0 8px 8px 0;padding:12px 16px;margin-bottom:14px}
.i-cs-ai-lbl{font-size:10px;font-weight:700;color:#6366f1;text-transform:uppercase;letter-spacing:.06em;margin-bottom:4px}
.i-cs-ai-text{font-size:12.5px;color:#334155;line-height:1.6}
.i-cs-prompt{text-align:center;padding:40px 24px;color:#94a3b8;font-size:12.5px;display:flex;flex-direction:column;align-items:center;gap:8px}
/* Keyword Map */
.i-km-wrap{position:relative;width:100%}
.i-km-controls{display:flex;align-items:center;gap:10px;margin-bottom:14px;flex-wrap:wrap}
.i-km-mode-grp{display:flex;gap:3px;background:#f1f5f9;border-radius:8px;padding:3px}
.i-km-mode-btn{padding:5px 12px;border-radius:6px;border:none;font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:11.5px;font-weight:600;color:#64748b;background:transparent;cursor:pointer;transition:all .14s}
.i-km-mode-btn.active{background:#fff;color:#1e293b;box-shadow:0 1px 4px rgba(0,0,0,.1)}
.i-km-legend{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-left:auto}
.i-km-legend-item{display:flex;align-items:center;gap:4px;font-size:10.5px;font-weight:600;color:#64748b}
.i-km-legend-dot{width:10px;height:10px;border-radius:50%;flex-shrink:0}
.i-km-svg-wrap{width:100%;background:#fafbff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;min-height:300px}
.i-km-svg-wrap svg{display:block;width:100%;height:auto}
.i-km-tooltip{position:fixed;background:#0f172a;color:#f1f5f9;border-radius:9px;padding:9px 13px;font-size:12px;pointer-events:none;z-index:9999;max-width:220px;box-shadow:0 8px 24px rgba(0,0,0,.22);transition:opacity .1s;opacity:0;line-height:1.5}
.i-km-tooltip.visible{opacity:1}
.i-km-tooltip-kw{font-weight:700;margin-bottom:3px}
.i-km-tooltip-meta{font-size:10.5px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.i-km-side{position:absolute;top:0;right:0;width:252px;background:#fff;border:1px solid #e2e8f0;border-radius:12px;box-shadow:0 8px 28px rgba(0,0,0,.1);padding:18px 18px;z-index:10;display:none}
.i-km-side.open{display:block}
.i-km-side-close{position:absolute;top:10px;right:12px;background:none;border:none;cursor:pointer;font-size:15px;color:#94a3b8;line-height:1}
.i-km-side-kw{font-size:14px;font-weight:700;color:#0f172a;margin-bottom:5px;padding-right:22px;line-height:1.3}
.i-km-side-meta{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace;margin-bottom:12px}
.i-km-side-rel-lbl{font-size:10px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;margin-bottom:7px}
.i-km-side-tags{display:flex;flex-wrap:wrap;gap:5px;margin-bottom:14px}
.i-km-side-tag{font-size:10.5px;font-weight:600;padding:3px 9px;background:#f1f5f9;border:1px solid #e2e8f0;border-radius:999px;color:#475569;cursor:pointer;transition:background .12s}
.i-km-side-tag:hover{background:#eef2ff;color:#6366f1;border-color:#c7d2fe}
.i-km-empty{display:flex;flex-direction:column;align-items:center;justify-content:center;min-height:300px;gap:9px;padding:32px}
  `;
  document.head.appendChild(s);
};

// ─── UI primitives ────────────────────────────────────────────────────────────

const mkField = ({ label, type = 'text', placeholder = '', options, value = '', badge = '' }) => {
  const wrap = el('div', { class: 'i-field' });
  const lblEl = el('span', { class: 'i-lbl' }, label);
  if (badge) lblEl.appendChild(el('span', { class: 'i-lbl-badge' }, badge));
  wrap.appendChild(lblEl);
  let input;
  if (options) {
    input = el('select', { class: 'i-select' });
    options.forEach(o => {
      const opt = el('option', { value: typeof o === 'string' ? o : o.value },
        typeof o === 'string' ? (o || 'Any') : o.label);
      if ((typeof o === 'string' ? o : o.value) === value) opt.selected = true;
      input.appendChild(opt);
    });
  } else {
    input = el('input', { class: 'i-input', type, placeholder });
    if (value) input.value = value;
  }
  wrap.appendChild(input);
  return { wrap, input };
};

const mkMetric = ({ icon, val, label, sub, accent = '#6366f1', light = '#eef2ff' }) => {
  const card = el('div', { class: 'i-metric', style: `--accent:${accent};--accent-light:${light}` });
  card.appendChild(el('div', { class: 'i-metric-icon' }, icon));
  const valEl = el('div', { class: 'i-metric-val' }, String(val ?? '—'));
  card.appendChild(valEl);
  card.appendChild(el('div', { class: 'i-metric-lbl' }, label));
  if (sub) card.appendChild(el('div', { class: 'i-metric-sub' }, sub));
  return { card, valEl };
};

const mkScoreBar = score => {
  const pct = Math.min(100, Math.max(0, Number(score) || 0));
  const fill = el('div', { class: 'i-score-fill', style: `width:0%;background:${scoreColor(pct)}` });
  const track = el('div', { class: 'i-score-track' }, fill);
  setTimeout(() => { fill.style.width = `${pct}%`; }, 60);
  return el('div', { class: 'i-score' }, el('span', { class: 'i-score-num' }, String(pct)), track);
};

const mkRank = rank => {
  const cls = rank === 1 ? 'i-rank-1' : rank === 2 ? 'i-rank-2' : rank === 3 ? 'i-rank-3' : 'i-rank-n';
  return el('span', { class: `i-rank ${cls}` }, rank != null ? `#${rank}` : '—');
};

const mkIntent = topic => {
  const intent = inferIntent(topic);
  const cfg    = INTENT[intent];
  const badge  = el('span', { class: 'i-intent', style: `background:${cfg.bg};color:${cfg.text}` });
  badge.appendChild(el('span', { class: 'i-intent-dot', style: `background:${cfg.dot}` }));
  badge.appendChild(document.createTextNode(intent));
  return badge;
};

const mkCanvas = ({ title, actions, noPad = false }) => {
  const wrap = el('div', { class: 'i-canvas i-fade' });
  const hd   = el('div', { class: 'i-canvas-hd' });
  hd.appendChild(el('div', { class: 'i-canvas-title' }, title));
  if (actions) hd.appendChild(actions);
  wrap.appendChild(hd);
  const body = el('div', { class: noPad ? '' : 'i-canvas-body' });
  if (!noPad) body.style.padding = '20px 22px';
  wrap.appendChild(body);
  return { wrap, body };
};

const buildTable = (items, { showRising = false } = {}) => {
  if (!items?.length) {
    return el('div', { class: 'i-empty' },
      el('div', { class: 'i-empty-icon' }, '📭'),
      el('div', { class: 'i-empty-title' }, 'No data found'),
      el('div', { class: 'i-empty-desc' }, 'Try different filters or refresh trend data.'));
  }
  const wrap = el('div', { class: 'i-table-wrap' });
  const table = el('table', { class: 'i-table' });
  const cols = ['#', 'Keyword / Topic', 'Score', 'Intent', 'Rank', 'Source'];
  if (showRising) cols.splice(3, 0, 'Trend');
  table.appendChild(el('thead', {}, el('tr', {}, ...cols.map(c => el('th', {}, c)))));
  const tbody = el('tbody', {});
  items.forEach((item, i) => {
    const tr = el('tr', {});
    tr.appendChild(el('td', {}, String(i + 1)));
    const tc = el('td', {});
    tc.appendChild(el('div', { class: 'i-topic-name' }, item.queryOrTopic || '—'));
    if (item.category) tc.appendChild(el('div', { class: 'i-topic-sub' }, item.category));
    tr.appendChild(tc);
    tr.appendChild(el('td', {}, mkScoreBar(item.score)));
    if (showRising) {
      const badge = item.isRising ? el('span', { class: 'i-rising' }, '↑ Rising') : el('span', { class: 'i-chip' }, 'Top');
      tr.appendChild(el('td', {}, badge));
    }
    tr.appendChild(el('td', {}, mkIntent(item.queryOrTopic)));
    tr.appendChild(el('td', {}, mkRank(item.rank)));
    tr.appendChild(el('td', {}, el('span', { class: 'i-chip' }, item.provider || 'GoogleTrends')));
    tbody.appendChild(tr);
  });
  table.appendChild(tbody);
  wrap.appendChild(table);
  return wrap;
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderIntelligenceView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    filters: { ...DEFAULT_FILTERS },
    sites: [], loading: false, profileLoading: false, refreshLoading: false,
    activeTab: 'signals', dashboard: null, hasEverLoaded: false, advancedOpen: false,
  };

  const root = el('div', { class: 'i-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'i-hero' });
  const eyebrow = el('div', { class: 'i-hero-eyebrow' }, el('span', { class: 'i-hero-pulse' }), 'Intelligence · Powered by Google Trends');
  hero.appendChild(eyebrow);
  hero.appendChild(el('h1', { class: 'i-hero-h1' }, 'See what your customers want ', el('em', {}, 'right now')));
  hero.appendChild(el('p', { class: 'i-hero-sub' }, 'Real-time Google Trends data filtered by your market, location, age group, and search type. Know what to stock, write, and sell before the moment passes.'));
  const heroStats = el('div', { class: 'i-hero-stats' });
  const mkHeroStat = label => { const w = el('div',{}); const v = el('div',{class:'i-stat-val'},'—'); w.append(v,el('div',{class:'i-stat-lbl'},label)); heroStats.appendChild(w); return v; };
  const heroTotal  = mkHeroStat('Trend Signals');
  const heroAvg    = mkHeroStat('Avg Score');
  const heroTop    = mkHeroStat('Top Keyword');
  const heroRising = mkHeroStat('Rising Now');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Tabs ───────────────────────────────────────────────────────────────────
  const tabBar = el('div', { class: 'i-tabs' });
  const TABS = [
    { key: 'signals',       label: '📈 Signals'      },
    { key: 'rising',        label: '🚀 Rising'        },
    { key: 'related',       label: '🔗 Related'       },
    { key: 'opportunities', label: '💡 Opportunities' },
    { key: 'keymap',        label: '🗺 Keyword Map'   },
    { key: 'network',       label: '🌐 Network'       },
    { key: 'competitors',   label: '🥊 Competitors'   },
    { key: 'profile',       label: '⚙️ Profile'       },
  ];
  const tabEls = {}; const panelEls = {};
  TABS.forEach(({ key, label }) => {
    const btn = el('button', { class: 'i-tab' + (key === state.activeTab ? ' active' : '') }, label);
    btn.addEventListener('click', () => switchTab(key));
    tabEls[key] = btn; tabBar.appendChild(btn);
  });
  root.appendChild(tabBar);
  TABS.forEach(({ key }) => {
    const p = el('div', {}); if (key !== state.activeTab) p.style.display = 'none';
    panelEls[key] = p; root.appendChild(p);
  });
  const switchTab = key => {
    state.activeTab = key;
    Object.entries(tabEls).forEach(([k,b]) => b.classList.toggle('active', k === key));
    Object.entries(panelEls).forEach(([k,p]) => { p.style.display = k === key ? 'block' : 'none'; });
  };

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: SIGNALS
  // ─────────────────────────────────────────────────────────────────────────
  const sigPanel = panelEls.signals;

  // Refresh bar
  const refreshBar = el('div', { class: 'i-refresh-bar', style: 'margin-bottom:16px' });
  const refreshInfo = el('div', { class: 'i-refresh-info' });
  const liveDot = el('div', { class: 'i-live-dot' });
  const refreshTxt = el('span', {}, 'Ready');
  const refreshAt  = el('span', { style: 'font-family:JetBrains Mono,monospace;font-size:10.5px;color:#94a3b8' }, '');
  refreshInfo.append(liveDot, refreshTxt, refreshAt);
  const refreshBtn = el('button', { class: 'i-btn i-btn-outline i-btn-sm' }, '↻ Refresh');
  refreshBar.append(refreshInfo, refreshBtn);
  sigPanel.appendChild(refreshBar);

  // Filter canvas
  const { wrap: filterWrap, body: filterBody } = mkCanvas({ title: '🔍 Search Filters' });

  // Core filters
  const filterGrid = el('div', { class: 'i-filters' });
  const { wrap: siteWrap, input: siteEl }  = mkField({ label: 'Site', options: [{ value: '', label: 'Loading…' }] });
  const { wrap: kwWrap,   input: kwEl   }  = mkField({ label: 'Keyword', placeholder: 'e.g. summer clothing' });
  const { wrap: catWrap,  input: catEl  }  = mkField({ label: 'Category', placeholder: 'e.g. Clothing' });
  const { wrap: locWrap,  input: locEl  }  = mkField({ label: 'Location', placeholder: 'e.g. Belfast' });
  const { wrap: twWrap,   input: twEl   }  = mkField({ label: 'Time Window', options: TIME_WINDOWS, value: '7d' });
  filterGrid.append(siteWrap, kwWrap, catWrap, locWrap, twWrap);
  filterBody.appendChild(filterGrid);

  // Advanced filters (collapsed by default)
  const advGrid = el('div', { class: 'i-filters-adv', style: 'display:none' });
  const { wrap: audWrap,    input: audEl    } = mkField({ label: 'Audience',      options: AUDIENCE_TYPES });
  const { wrap: ageWrap,    input: ageEl    } = mkField({ label: 'Age Range',     options: AGE_RANGES });
  const { wrap: searchWrap, input: searchEl } = mkField({ label: 'Search Type',   options: SEARCH_TYPES });
  const { wrap: gcatWrap,   input: gcatEl   } = mkField({ label: 'Google Category', options: GOOGLE_CATEGORIES, badge: 'Trends' });
  const { wrap: srWrap,     input: srEl     } = mkField({ label: 'Sub-Region',    options: SUB_REGIONS });
  const { wrap: compWrap,   input: compEl   } = mkField({ label: 'Compare Terms', placeholder: 'e.g. winter boots, coats', badge: 'up to 4' });
  advGrid.append(audWrap, ageWrap, searchWrap, gcatWrap, srWrap, compWrap);
  filterBody.appendChild(advGrid);

  const filterRow = el('div', { class: 'i-filter-row' });
  const applyBtn  = el('button', { class: 'i-btn i-btn-primary' }, '⚡ Search Trends');
  const resetBtn  = el('button', { class: 'i-btn i-btn-outline' }, 'Reset');
  const advBtn    = el('button', { class: 'i-adv-toggle' }, '▸ Advanced filters');
  advBtn.addEventListener('click', () => {
    state.advancedOpen = !state.advancedOpen;
    advGrid.style.display = state.advancedOpen ? 'grid' : 'none';
    advBtn.textContent = (state.advancedOpen ? '▾ ' : '▸ ') + 'Advanced filters';
  });
  filterRow.append(applyBtn, resetBtn, advBtn);
  filterBody.appendChild(filterRow);
  sigPanel.appendChild(filterWrap);

  // Onboarding banner
  const onboard = el('div', { class: 'i-onboard i-fade' });
  onboard.innerHTML = `
    <div class="i-onboard-icon">📡</div>
    <div class="i-onboard-title">Your Intelligence Canvas is ready</div>
    <div class="i-onboard-desc">Find out what your customers are searching for — before they visit your website. Enter a keyword and location, then click <strong>Search Trends</strong>.</div>
    <div class="i-onboard-steps">
      <div class="i-onboard-step"><div class="i-onboard-step-num">1</div><span>Select your site</span></div>
      <div class="i-onboard-step"><div class="i-onboard-step-num">2</div><span>Enter keyword + location</span></div>
      <div class="i-onboard-step"><div class="i-onboard-step-num">3</div><span>Click Search Trends</span></div>
      <div class="i-onboard-step"><div class="i-onboard-step-num">4</div><span>See real demand data</span></div>
    </div>`;
  sigPanel.appendChild(onboard);

  // AI summary
  const aiBox    = el('div', { class: 'i-ai-box i-fade', style: 'display:none' });
  const aiLabel  = el('div', { class: 'i-ai-label' }, el('span',{},'✦'), 'AI Market Insight');
  const aiText   = el('div', { class: 'i-ai-text' }, '');
  const aiFooter = el('div', { class: 'i-ai-footer' }, '');
  aiBox.append(aiLabel, aiText, aiFooter);
  sigPanel.appendChild(aiBox);

  // Metrics
  const metricsRow = el('div', { class: 'i-metrics', style: 'display:none' });
  const { card: mTotalCard,  valEl: mTotalVal  } = mkMetric({ icon: '📊', val: '—', label: 'Trend Signals',    accent: '#6366f1', light: '#eef2ff' });
  const { card: mAvgCard,    valEl: mAvgVal    } = mkMetric({ icon: '⭐', val: '—', label: 'Avg Score',        accent: '#f59e0b', light: '#fef3c7' });
  const { card: mRisingCard, valEl: mRisingVal } = mkMetric({ icon: '🚀', val: '—', label: 'Rising Searches',  accent: '#10b981', light: '#d1fae5' });
  const { card: mRelCard,    valEl: mRelVal    } = mkMetric({ icon: '🔗', val: '—', label: 'Related Queries',  accent: '#3b82f6', light: '#dbeafe' });
  metricsRow.append(mTotalCard, mAvgCard, mRisingCard, mRelCard);
  sigPanel.appendChild(metricsRow);

  // Trends table
  const { wrap: trendsWrap, body: trendsBody } = mkCanvas({ title: '📈 Top Trend Signals', noPad: true });
  trendsBody.style.padding = '0'; trendsBody.style.display = 'none';
  sigPanel.appendChild(trendsWrap);

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: RISING
  // ─────────────────────────────────────────────────────────────────────────
  const risingPanel = panelEls.rising;
  const { wrap: risingWrap, body: risingBody } = mkCanvas({ title: '🚀 Rising Searches — Surging Right Now', noPad: true });
  risingBody.style.padding = '0';
  risingBody.innerHTML = '<div class="i-empty"><div class="i-empty-icon">🚀</div><div class="i-empty-title">No data yet</div><div class="i-empty-desc">Load trend data from the Signals tab first.</div></div>';
  risingPanel.appendChild(risingWrap);

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: RELATED
  // ─────────────────────────────────────────────────────────────────────────
  const relatedPanel = panelEls.related;
  const { wrap: relatedWrap, body: relatedBody } = mkCanvas({ title: '🔗 Related Searches — What else they\'re looking for', noPad: true });
  relatedBody.style.padding = '0';
  relatedBody.innerHTML = '<div class="i-empty"><div class="i-empty-icon">🔗</div><div class="i-empty-title">No data yet</div><div class="i-empty-desc">Load trend data from the Signals tab first.</div></div>';
  relatedPanel.appendChild(relatedWrap);

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: OPPORTUNITIES
  // ─────────────────────────────────────────────────────────────────────────
  const oppsPanel = panelEls.opportunities;
  const { wrap: oppsWrap, body: oppsBody } = mkCanvas({ title: '💡 Detected Opportunities' });
  oppsBody.innerHTML = '<div class="i-empty"><div class="i-empty-icon">💡</div><div class="i-empty-title">No opportunities yet</div><div class="i-empty-desc">Load trend data from the Signals tab to generate insights.</div></div>';
  oppsPanel.appendChild(oppsWrap);

  // ─────────────────────────────────────────────────────────────────────────
  // ─────────────────────────────────────────────────────────────────────────
  // TAB: NETWORK SIGNALS
  // ─────────────────────────────────────────────────────────────────────────
  const networkPanel = panelEls.network;
  const nsState = { loading: false, data: null, country: '', category: '', daysBack: 7 };

  const COUNTRY_OPTIONS = [
    { value: '', label: 'All countries' },
    { value: 'United Kingdom', label: '🇬🇧 United Kingdom' },
    { value: 'United States', label: '🇺🇸 United States' },
    { value: 'Ireland', label: '🇮🇪 Ireland' },
    { value: 'Germany', label: '🇩🇪 Germany' },
    { value: 'France', label: '🇫🇷 France' },
    { value: 'Australia', label: '🇦🇺 Australia' },
    { value: 'Canada', label: '🇨🇦 Canada' },
    { value: 'India', label: '🇮🇳 India' },
    { value: 'Netherlands', label: '🇳🇱 Netherlands' },
    { value: 'Spain', label: '🇪🇸 Spain' },
  ];

  const COUNTRY_FLAGS = {
    'United Kingdom': '🇬🇧', 'United States': '🇺🇸', 'Ireland': '🇮🇪',
    'Germany': '🇩🇪', 'France': '🇫🇷', 'Australia': '🇦🇺', 'Canada': '🇨🇦',
    'India': '🇮🇳', 'Netherlands': '🇳🇱', 'Spain': '🇪🇸', 'Italy': '🇮🇹',
    'Brazil': '🇧🇷', 'Japan': '🇯🇵', 'Singapore': '🇸🇬',
  };

  // ── Network header ─────────────────────────────────────────────────────
  const nsHeader = el('div', { style: 'margin-bottom:16px' });
  nsHeader.appendChild(el('div', { style: 'font-size:16px;font-weight:700;color:#0f172a;margin-bottom:4px' }, '🌐 Hven Network Signals'));
  nsHeader.appendChild(el('div', { style: 'font-size:12.5px;color:#64748b;line-height:1.6' }, 'Anonymous intent trends from across the Hven network — aggregated from all sites, no individual data'));
  networkPanel.appendChild(nsHeader);

  // ── Network filters ────────────────────────────────────────────────────
  const nsFiltersRow = el('div', { class: 'i-ns-filters' });
  const nsMkSelect = (opts, onChange) => {
    const s = el('select', { class: 'i-select', style: 'width:auto;min-width:140px' });
    opts.forEach(o => s.appendChild(el('option', { value: o.value }, o.label)));
    s.addEventListener('change', () => onChange(s.value));
    return s;
  };
  const nsCountrySel  = nsMkSelect(COUNTRY_OPTIONS,
    v => { nsState.country = v; loadNetworkSignals(); });
  const nsCategorySel = nsMkSelect(
    [{ value: '', label: 'All categories' }, ...GOOGLE_CATEGORIES.filter(c => c.value === '' || true).map(c => ({ value: c.label, label: c.label }))],
    v => { nsState.category = v; loadNetworkSignals(); });
  const nsDaysSel = nsMkSelect(
    [{ value: '7', label: 'Past 7 days' }, { value: '14', label: 'Past 14 days' }, { value: '30', label: 'Past 30 days' }],
    v => { nsState.daysBack = parseInt(v, 10); loadNetworkSignals(); });
  nsFiltersRow.append(
    el('div', { style: 'display:flex;flex-direction:column;gap:3px' }, el('span', { class: 'i-lbl' }, 'Country'), nsCountrySel),
    el('div', { style: 'display:flex;flex-direction:column;gap:3px' }, el('span', { class: 'i-lbl' }, 'Category'), nsCategorySel),
    el('div', { style: 'display:flex;flex-direction:column;gap:3px' }, el('span', { class: 'i-lbl' }, 'Period'), nsDaysSel),
  );
  networkPanel.appendChild(nsFiltersRow);

  // ── Content area ────────────────────────────────────────────────────────
  const nsContent = el('div', {});
  networkPanel.appendChild(nsContent);

  const renderNetworkSkeletons = () => {
    nsContent.replaceChildren();
    const grid = el('div', { class: 'i-ns-grid' });
    for (let i = 0; i < 3; i++) {
      const p = el('div', { class: 'i-ns-panel' });
      p.appendChild(el('div', { class: 'i-ns-panel-hd i-skel', style: 'width:60%;height:16px;margin:12px 16px' }));
      const body = el('div', { class: 'i-ns-panel-body' });
      for (let j = 0; j < 5; j++) body.appendChild(el('div', { class: 'i-skel i-ns-skel' }));
      p.appendChild(body);
      grid.appendChild(p);
    }
    nsContent.appendChild(grid);
  };

  const renderNetworkSignals = data => {
    nsContent.replaceChildren();
    if (!data) return;

    const grid = el('div', { class: 'i-ns-grid' });

    // Panel 1: Trending Topics bar chart
    const p1 = el('div', { class: 'i-ns-panel' });
    p1.appendChild(el('div', { class: 'i-ns-panel-hd' }, '📈 Trending Topics'));
    const p1body = el('div', { class: 'i-ns-panel-body' });
    if (data.trendingTopics?.length) {
      data.trendingTopics.forEach(t => {
        const row = el('div', { class: 'i-ns-bar-row' });
        row.appendChild(el('div', { class: 'i-ns-bar-label' },
          el('span', {}, t.topic),
          el('span', { style: 'font-size:10px;color:#94a3b8' }, String(t.signalCount))
        ));
        const track = el('div', { class: 'i-ns-bar-track' });
        const fill  = el('div', { class: 'i-ns-bar-fill' });
        fill.style.width = Math.round((t.trendScore || 0) * 100) + '%';
        track.appendChild(fill);
        row.appendChild(track);
        p1body.appendChild(row);
      });
    } else {
      p1body.appendChild(el('div', { style: 'font-size:12px;color:#94a3b8;text-align:center;padding:16px 0' }, 'No topic signals yet'));
    }
    p1.appendChild(p1body);
    grid.appendChild(p1);

    // Panel 2: Category intents as pills
    const p2 = el('div', { class: 'i-ns-panel' });
    p2.appendChild(el('div', { class: 'i-ns-panel-hd' }, '🏷 Top Categories by Intent'));
    const p2body = el('div', { class: 'i-ns-panel-body', style: 'flex-direction:row;flex-wrap:wrap;gap:6px' });
    if (data.categoryIntents?.length) {
      data.categoryIntents.forEach(c => {
        const hot = (c.intentScore || 0) > 0.7;
        const pill = el('span', { class: 'i-ns-cat-pill ' + (hot ? 'i-ns-cat-hot' : 'i-ns-cat-norm') },
          c.category,
          el('span', { style: 'font-size:9px;opacity:.7;margin-left:3px' }, String(c.visitorCount))
        );
        p2body.appendChild(pill);
      });
    } else {
      p2body.appendChild(el('div', { style: 'font-size:12px;color:#94a3b8;text-align:center;padding:16px 0;width:100%' }, 'No category signals yet'));
    }
    p2.appendChild(p2body);
    grid.appendChild(p2);

    // Panel 3: Geographic signals
    const p3 = el('div', { class: 'i-ns-panel' });
    p3.appendChild(el('div', { class: 'i-ns-panel-hd' }, '🗺 Geographic Signals'));
    const p3body = el('div', { class: 'i-ns-panel-body' });
    if (data.countryIntents?.length) {
      data.countryIntents.forEach(c => {
        const flag = COUNTRY_FLAGS[c.country] || '🌍';
        const row = el('div', { class: 'i-ns-country-row' },
          el('span', {}, flag + ' ' + c.country),
          el('span', { class: 'i-ns-country-count' }, String(c.visitorCount) + ' visits')
        );
        p3body.appendChild(row);
      });
    } else {
      p3body.appendChild(el('div', { style: 'font-size:12px;color:#94a3b8;text-align:center;padding:16px 0' }, 'No geographic signals yet'));
    }
    p3.appendChild(p3body);
    grid.appendChild(p3);

    nsContent.appendChild(grid);

    // Product trends row
    if (data.productTrends?.length) {
      const ptSection = el('div', { style: 'margin-top:16px' });
      ptSection.appendChild(el('div', { style: 'font-size:12px;font-weight:700;color:#0f172a;margin-bottom:10px' }, '🛍 Trending Products'));
      const scroll = el('div', { class: 'i-ns-product-scroll' });
      data.productTrends.forEach(p => {
        const card = el('div', { class: 'i-ns-product-card' });
        card.appendChild(el('div', { class: 'i-ns-product-name', title: p.productName }, p.productName));
        const badges = el('div', { class: 'i-ns-product-badges' });
        if (p.category) badges.appendChild(el('span', { class: 'i-ns-product-badge', style: 'background:#eef2ff;color:#4338ca;border-color:#c7d2fe' }, p.category));
        if (p.priceRange) badges.appendChild(el('span', { class: 'i-ns-product-badge', style: 'background:#f0fdf4;color:#166534;border-color:#bbf7d0' }, p.priceRange));
        badges.appendChild(el('span', { class: 'i-ns-product-badge', style: 'background:#f1f5f9;color:#64748b;border-color:#e2e8f0' }, String(p.viewCount) + ' views'));
        card.appendChild(badges);
        scroll.appendChild(card);
      });
      ptSection.appendChild(scroll);
      nsContent.appendChild(ptSection);
    }

    // Footer attribution
    const ago = data.generatedAtUtc ? fmtTimeAgo(data.generatedAtUtc) : '';
    const footer = el('div', { class: 'i-ns-footer' },
      'Based on signals from ',
      el('strong', {}, String(data.totalSitesContributing || 0)), ' sites · ',
      el('strong', {}, String(data.totalVisitorsContributing || 0)), ' visits · ',
      'Last ', el('strong', {}, String(nsState.daysBack)), ' days',
      ago ? (' · Updated ' + ago) : ''
    );
    nsContent.appendChild(footer);
  };

  const loadNetworkSignals = async () => {
    if (nsState.loading) return;
    nsState.loading = true;
    renderNetworkSkeletons();
    try {
      const data = await client.intelligence.getNetworkSignals(
        nsState.country || undefined,
        nsState.category || undefined,
        nsState.daysBack);
      nsState.data = data;
      renderNetworkSignals(data);
    } catch (err) {
      nsContent.replaceChildren();
      nsContent.appendChild(el('div', { class: 'i-empty' },
        el('div', { class: 'i-empty-icon' }, '🌐'),
        el('div', { class: 'i-empty-title' }, 'Network signals unavailable'),
        el('div', { class: 'i-empty-desc' }, 'No signal data yet — this updates as visitors browse sites using the Hven tracker.')
      ));
    } finally {
      nsState.loading = false;
    }
  };

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: COMPETITORS
  // ─────────────────────────────────────────────────────────────────────────
  const competitorsPanel = panelEls.competitors;

  const csState = { industry: '', location: '', timeWindow: '7d', data: null, loading: false };

  // ── Filters ───────────────────────────────────────────────────────────────
  const csFilters = el('div', { class: 'i-ns-filters', style: 'margin-bottom:14px' });
  const csIndustryEl = el('input', { type: 'text', class: 'i-field', placeholder: 'e.g. fashion, cybersecurity, real estate', style: 'flex:1;min-width:160px' });
  csIndustryEl.addEventListener('input', () => { csState.industry = csIndustryEl.value.trim(); });
  csIndustryEl.addEventListener('change', () => { if (csState.industry) loadCompetitorSignals(); });

  const csLocationEl = el('select', { class: 'i-select' });
  [{ value: 'GB', label: '🇬🇧 United Kingdom' }, { value: 'IE', label: '🇮🇪 Ireland' }, { value: 'US', label: '🇺🇸 United States' }, { value: 'AU', label: '🇦🇺 Australia' }, { value: 'CA', label: '🇨🇦 Canada' }, { value: 'DE', label: '🇩🇪 Germany' }, { value: 'FR', label: '🇫🇷 France' }]
    .forEach(opt => csLocationEl.appendChild(el('option', { value: opt.value }, opt.label)));
  csLocationEl.addEventListener('change', () => { csState.location = csLocationEl.value; if (csState.industry) loadCompetitorSignals(); });

  const csTwEl = el('select', { class: 'i-select' });
  [{ value: '7d', label: 'Past 7 days' }, { value: '30d', label: 'Past 30 days' }, { value: '90d', label: 'Past 90 days' }]
    .forEach(opt => csTwEl.appendChild(el('option', { value: opt.value }, opt.label)));
  csTwEl.addEventListener('change', () => { csState.timeWindow = csTwEl.value; if (csState.industry) loadCompetitorSignals(); });

  const csGoBtn = el('button', { class: 'i-btn i-btn-primary' }, '🔍 Analyse');
  csGoBtn.addEventListener('click', () => { if (csState.industry) loadCompetitorSignals(); });

  csFilters.append(csIndustryEl, csLocationEl, csTwEl, csGoBtn);
  competitorsPanel.appendChild(csFilters);

  const csContent = el('div', {});
  competitorsPanel.appendChild(csContent);

  // ── Skeletons ─────────────────────────────────────────────────────────────
  const renderCompetitorSkeletons = () => {
    csContent.replaceChildren();
    const skGrid = el('div', { class: 'i-cs-grid' });
    [0, 1].forEach(() => {
      const col = el('div', {});
      for (let i = 0; i < 5; i++) col.appendChild(el('div', { class: 'i-skel i-ns-skel', style: 'margin-bottom:6px' }));
      skGrid.appendChild(col);
    });
    csContent.appendChild(skGrid);
  };

  // ── Intent badge ──────────────────────────────────────────────────────────
  const mkIntentBadge = intent => {
    const cls = intent === 'Transactional' ? 'i-cs-intent i-cs-intent-t'
              : intent === 'Commercial'    ? 'i-cs-intent i-cs-intent-c'
              :                              'i-cs-intent i-cs-intent-i';
    return el('span', { class: cls }, intent || 'Informational');
  };

  // ── Keyword card ──────────────────────────────────────────────────────────
  const mkKeywordCard = (kw, rising) => {
    const card = el('div', { class: rising ? 'i-cs-card i-cs-card-rising' : 'i-cs-card' });
    const kwRow = el('div', { class: 'i-cs-kw' });
    if (rising) kwRow.appendChild(el('span', { style: 'color:#10b981;font-size:13px' }, '↑'));
    kwRow.appendChild(el('span', {}, kw.keyword));
    kwRow.appendChild(mkIntentBadge(kw.intent));
    card.appendChild(kwRow);
    const barColor = kw.intent === 'Transactional' ? '#10b981' : kw.intent === 'Commercial' ? '#3b82f6' : '#6366f1';
    const barWrap = el('div', { class: 'i-cs-bar-wrap' });
    const track = el('div', { class: 'i-cs-bar-track' });
    const fill  = el('div', { class: 'i-cs-bar-fill', style: `background:${barColor};width:0%` });
    track.appendChild(fill);
    barWrap.append(track, el('span', { class: 'i-cs-bar-val' }, String(kw.score)));
    card.appendChild(barWrap);
    setTimeout(() => { fill.style.width = `${kw.score}%`; }, 80);
    return card;
  };

  // ── Render ────────────────────────────────────────────────────────────────
  const renderCompetitorSignals = data => {
    csContent.replaceChildren();
    if (!data.trendingKeywords?.length && !data.risingKeywords?.length) {
      csContent.appendChild(el('div', { class: 'i-cs-prompt' },
        el('div', { style: 'font-size:28px;opacity:.35' }, '🥊'),
        el('div', { style: 'font-weight:700;color:#334155' }, 'No signals found'),
        el('div', {}, 'Try a different industry term or broader location.')));
      return;
    }

    if (data.aiSummary) {
      const aiBox = el('div', { class: 'i-cs-ai-box i-fade' });
      aiBox.appendChild(el('div', { class: 'i-cs-ai-lbl' }, '🤖 AI Analysis'));
      aiBox.appendChild(el('div', { class: 'i-cs-ai-text' }, data.aiSummary));
      csContent.appendChild(aiBox);
    }

    const grid = el('div', { class: 'i-cs-grid i-fade' });

    // Left: trending
    const leftCol = el('div', {});
    leftCol.appendChild(el('div', { class: 'i-cs-col-hd' }, '📊 What people are searching for in your industry'));
    if (data.trendingKeywords?.length) {
      data.trendingKeywords.forEach(kw => leftCol.appendChild(mkKeywordCard(kw, false)));
    } else {
      leftCol.appendChild(el('div', { style: 'color:#94a3b8;font-size:12px;padding:8px 0' }, 'No trending keywords found.'));
    }
    grid.appendChild(leftCol);

    // Right: rising
    const rightCol = el('div', {});
    rightCol.appendChild(el('div', { class: 'i-cs-col-hd' }, '🚀 Rising fast this week'));
    if (data.risingKeywords?.length) {
      data.risingKeywords.forEach(kw => rightCol.appendChild(mkKeywordCard(kw, true)));
    } else {
      rightCol.appendChild(el('div', { style: 'color:#94a3b8;font-size:12px;padding:8px 0' }, 'No rising keywords found.'));
    }
    grid.appendChild(rightCol);

    csContent.appendChild(grid);
  };

  // ── Load ──────────────────────────────────────────────────────────────────
  const loadCompetitorSignals = async () => {
    if (csState.loading) return;
    if (!csState.industry) {
      csContent.replaceChildren(el('div', { class: 'i-cs-prompt' },
        el('div', { style: 'font-size:28px;opacity:.35' }, '🥊'),
        el('div', { style: 'font-weight:700;color:#334155' }, 'Enter your industry above'),
        el('div', {}, 'Type your industry keyword and click Analyse to see competitor signals.')));
      return;
    }
    csState.loading = true;
    renderCompetitorSkeletons();
    try {
      const data = await client.intelligence.getCompetitorSignals(
        csState.industry,
        csState.location || csLocationEl.value,
        csState.timeWindow || csTwEl.value);
      csState.data = data;
      renderCompetitorSignals(data);
    } catch (err) {
      csContent.replaceChildren(el('div', { class: 'i-empty' },
        el('div', { class: 'i-empty-icon' }, '⚠️'),
        el('div', { class: 'i-empty-title' }, 'Could not load competitor signals'),
        el('div', { class: 'i-empty-desc' }, mapApiError(err).message || 'Check your SerpApi configuration.')));
    } finally {
      csState.loading = false;
    }
  };

  // Show initial prompt
  csContent.appendChild(el('div', { class: 'i-cs-prompt' },
    el('div', { style: 'font-size:28px;opacity:.35' }, '🥊'),
    el('div', { style: 'font-weight:700;color:#334155' }, 'Enter your industry above'),
    el('div', {}, 'Type your industry keyword and click Analyse to see competitor signals.')));

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: KEYWORD MAP
  // ─────────────────────────────────────────────────────────────────────────
  const keymapPanel = panelEls.keymap;
  const kmState = { mode: 'site', rendered: false };

  const KM_COLORS = {
    Transactional: '#10b981',
    Commercial:    '#6366f1',
    Informational: '#f59e0b',
    Network:       '#0ea5e9',
  };

  const layoutBubbles = (items, svgW = 800, svgH = 480) => {
    if (!items.length) return [];
    const goldenAngle = 2.399;
    const spacing     = 68;
    const cx = svgW / 2, cy = svgH / 2;
    const padX = 64, padY = 52;
    const sorted = [...items].sort((a, b) => b.r - a.r);
    return sorted.map((item, i) => {
      if (i === 0) return { ...item, x: cx, y: cy };
      const angle = i * goldenAngle;
      const dist  = Math.sqrt(i) * spacing;
      const rawX  = cx + Math.cos(angle) * dist;
      const rawY  = cy + Math.sin(angle) * dist;
      const x = Math.max(padX + item.r, Math.min(svgW - padX - item.r, rawX));
      const y = Math.max(padY + item.r, Math.min(svgH - padY - item.r, rawY));
      return { ...item, x, y };
    });
  };

  const buildBubbleItems = mode => {
    const items = [];
    const seen  = new Set();
    if (mode === 'site' || mode === 'both') {
      const all = [...(state.dashboard?.topItems || []), ...(state.dashboard?.risingQueries || [])];
      all.forEach(item => {
        const key = (item.queryOrTopic || '').toLowerCase();
        if (seen.has(key)) return; seen.add(key);
        const score = Number(item.score) || 0;
        items.push({ keyword: item.queryOrTopic, score, intent: inferIntent(item.queryOrTopic),
          r: 20 + (score / 100) * 40, source: 'site', isRising: !!item.isRising });
      });
    }
    if (mode === 'network' || mode === 'both') {
      (nsState.data?.topSearches || []).forEach(item => {
        const key = (item.term || '').toLowerCase();
        if (seen.has(key)) return; seen.add(key);
        const score = Number(item.trendScore) || Number(item.score) || 50;
        items.push({ keyword: item.term, score, intent: 'Network',
          r: 20 + (score / 100) * 40, source: 'network', isRising: false });
      });
    }
    return items;
  };

  // Shared tooltip appended to body
  const kmTooltip = el('div', { class: 'i-km-tooltip' });
  document.body.appendChild(kmTooltip);
  const kmShowTip  = (e, item) => {
    kmTooltip.replaceChildren(
      el('div', { class: 'i-km-tooltip-kw' }, item.keyword),
      el('div', { class: 'i-km-tooltip-meta' }, `Score: ${item.score}  ·  ${item.intent}${item.isRising ? '  · ↑ Rising' : ''}`));
    kmTooltip.classList.add('visible');
    kmMoveTip(e);
  };
  const kmMoveTip  = e => {
    kmTooltip.style.left = `${e.clientX + 14}px`;
    kmTooltip.style.top  = `${e.clientY - 28}px`;
  };
  const kmHideTip  = () => kmTooltip.classList.remove('visible');

  // Side panel
  const kmSide      = el('div', { class: 'i-km-side' });
  const kmSideClose = el('button', { class: 'i-km-side-close' }, '✕');
  const kmSideKw    = el('div', { class: 'i-km-side-kw' });
  const kmSideMeta  = el('div', { class: 'i-km-side-meta' });
  const kmSideRelLbl = el('div', { class: 'i-km-side-rel-lbl' }, 'Related Searches');
  const kmSideTags  = el('div', { class: 'i-km-side-tags' });
  const kmSideBtn   = el('button', { class: 'i-btn i-btn-primary i-btn-sm', style: 'width:100%' }, '🔍 Search this trend');
  kmSideClose.addEventListener('click', () => kmSide.classList.remove('open'));
  kmSide.append(kmSideClose, kmSideKw, kmSideMeta, kmSideRelLbl, kmSideTags, kmSideBtn);

  const kmShowSide = item => {
    kmSideKw.textContent   = item.keyword;
    kmSideMeta.textContent = `Score ${item.score}/100  ·  ${item.intent}${item.isRising ? '  ·  ↑ Rising' : ''}`;
    kmSideTags.replaceChildren();
    const related = [
      ...(state.dashboard?.relatedQueries || []),
      ...(state.dashboard?.topItems || []),
    ].filter(r => r.queryOrTopic && r.queryOrTopic.toLowerCase() !== item.keyword.toLowerCase()).slice(0, 8);
    if (related.length) {
      related.forEach(r => {
        const tag = el('span', { class: 'i-km-side-tag' }, r.queryOrTopic);
        tag.addEventListener('click', () => {
          kmSideKw.textContent   = r.queryOrTopic;
          kmSideMeta.textContent = `Score ${Number(r.score) || 0}/100  ·  ${inferIntent(r.queryOrTopic)}`;
        });
        kmSideTags.appendChild(tag);
      });
    } else {
      kmSideTags.appendChild(el('span', { style: 'color:#94a3b8;font-size:11px' }, 'No related searches loaded yet.'));
    }
    kmSideBtn.onclick = () => {
      kwEl.value = item.keyword;
      switchTab('signals');
      loadDashboard();
    };
    kmSide.classList.add('open');
  };

  const svgNS = 'http://www.w3.org/2000/svg';
  const svgEl = (tag, attrs = {}) => {
    const e = document.createElementNS(svgNS, tag);
    Object.entries(attrs).forEach(([k, v]) => e.setAttribute(k, String(v)));
    return e;
  };

  const SVG_W = 800, SVG_H = 480;
  const kmSvgWrap = el('div', { class: 'i-km-svg-wrap' });

  const renderKeymap = () => {
    kmSide.classList.remove('open');
    const items = buildBubbleItems(kmState.mode);
    kmSvgWrap.replaceChildren();

    if (!items.length) {
      kmSvgWrap.appendChild(el('div', { class: 'i-km-empty' },
        el('div', { style: 'font-size:40px;opacity:.3' }, '🗺'),
        el('div', { style: 'font-size:14px;font-weight:700;color:#334155' }, 'No keyword data yet'),
        el('div', { style: 'font-size:12px;color:#94a3b8;max-width:260px;text-align:center;line-height:1.6' },
          'Run a Trends search first, or switch to Network mode to see market-wide signals.')));
      return;
    }

    const svg  = svgEl('svg', { viewBox: `0 0 ${SVG_W} ${SVG_H}`, xmlns: svgNS });
    // Subtle background dots
    for (let gx = 40; gx < SVG_W; gx += 60) {
      for (let gy = 30; gy < SVG_H; gy += 55) {
        svg.appendChild(svgEl('circle', { cx: gx, cy: gy, r: 1.2, fill: '#e2e8f0' }));
      }
    }

    const laid = layoutBubbles(items, SVG_W, SVG_H);
    laid.forEach(item => {
      const color  = KM_COLORS[item.intent] || '#6366f1';
      const g      = svgEl('g', { style: 'cursor:pointer' });
      g.appendChild(svgEl('circle', {
        cx: item.x, cy: item.y, r: item.r,
        fill: color, 'fill-opacity': 0.18,
        stroke: color, 'stroke-width': item.isRising ? 2.5 : 1.5,
      }));
      if (item.r >= 28) {
        const maxChars = Math.floor(item.r / 4.2);
        const lbl      = item.keyword.length > maxChars ? item.keyword.slice(0, maxChars) + '…' : item.keyword;
        const t = svgEl('text', {
          x: item.x, y: item.y + 1,
          'text-anchor': 'middle', 'dominant-baseline': 'middle',
          fill: color, 'font-size': Math.max(9, Math.min(12, item.r / 3.5)),
          'font-weight': 600, 'font-family': 'Plus Jakarta Sans, system-ui, sans-serif',
        });
        t.textContent = lbl;
        g.appendChild(t);
      }
      if (item.isRising) {
        const arrow = svgEl('text', { x: item.x + item.r - 4, y: item.y - item.r + 6, 'font-size': 10, fill: color, 'font-weight': 700 });
        arrow.textContent = '↑';
        g.appendChild(arrow);
      }
      g.addEventListener('mouseenter', e => kmShowTip(e, item));
      g.addEventListener('mousemove', kmMoveTip);
      g.addEventListener('mouseleave', kmHideTip);
      g.addEventListener('click', () => { kmHideTip(); kmShowSide(item); });
      svg.appendChild(g);
    });
    kmSvgWrap.appendChild(svg);
  };

  // Mode controls
  const kmControls = el('div', { class: 'i-km-controls' });
  const kmModeGrp  = el('div', { class: 'i-km-mode-grp' });
  [{ key: 'site', label: '📊 Your Site' }, { key: 'network', label: '🌐 Network' }, { key: 'both', label: '✦ Both' }].forEach(({ key, label }) => {
    const btn = el('button', { class: 'i-km-mode-btn' + (key === kmState.mode ? ' active' : '') }, label);
    btn.addEventListener('click', () => {
      kmState.mode = key;
      kmModeGrp.querySelectorAll('.i-km-mode-btn').forEach(b => b.classList.toggle('active', b === btn));
      renderKeymap();
    });
    kmModeGrp.appendChild(btn);
  });
  const kmRefreshBtn = el('button', { class: 'i-btn i-btn-outline i-btn-sm' }, '↻ Refresh');
  kmRefreshBtn.addEventListener('click', renderKeymap);
  const kmLegend = el('div', { class: 'i-km-legend' });
  [['#10b981', 'Transactional'], ['#6366f1', 'Commercial'], ['#f59e0b', 'Informational'], ['#0ea5e9', 'Network']].forEach(([color, lbl]) => {
    const li = el('div', { class: 'i-km-legend-item' });
    li.append(el('div', { class: 'i-km-legend-dot', style: `background:${color}` }), lbl);
    kmLegend.appendChild(li);
  });
  kmControls.append(kmModeGrp, kmRefreshBtn, kmLegend);

  const kmWrap = el('div', { class: 'i-km-wrap' });
  kmWrap.append(kmSvgWrap, kmSide);

  const { wrap: keymapWrap, body: keymapBody } = mkCanvas({ title: '🗺 Keyword Cluster Map', noPad: true });
  keymapBody.style.padding = '16px 18px';
  keymapBody.append(kmControls, kmWrap);
  keymapPanel.appendChild(keymapWrap);

  const loadKeymap = () => { if (!kmState.rendered) { kmState.rendered = true; renderKeymap(); } };

  // ─────────────────────────────────────────────────────────────────────────
  // TAB: PROFILE
  // ─────────────────────────────────────────────────────────────────────────
  const profilePanel = panelEls.profile;
  const profileStatusRow = el('div', { style: 'display:flex;align-items:center;gap:8px;margin-bottom:16px' });
  const profilePill = el('span', { class: 'i-pill i-pill-inactive' }, '⬤ No profile loaded');
  profileStatusRow.appendChild(profilePill);
  profilePanel.appendChild(profileStatusRow);

  const { wrap: profileWrap, body: profileBody } = mkCanvas({ title: '⚙️ Intelligence Profile' });
  const profileGrid = el('div', { class: 'i-profile-grid' });
  const { wrap: pNameWrap,    input: pNameEl    } = mkField({ label: 'Profile Name', placeholder: 'My Business' });
  const { wrap: pInduWrap,    input: pInduEl    } = mkField({ label: 'Industry', placeholder: 'Clothing, Retail…' });
  const { wrap: pAudWrap,     input: pAudEl     } = mkField({ label: 'Audience Type', options: AUDIENCE_TYPES });
  const { wrap: pLocWrap,     input: pLocEl     } = mkField({ label: 'Target Locations', placeholder: 'Belfast, Dublin' });
  const { wrap: pProdWrap,    input: pProdEl    } = mkField({ label: 'Products / Services', placeholder: 'Dresses, Jackets' });
  const { wrap: pWatchWrap,   input: pWatchEl   } = mkField({ label: 'Watch Topics', placeholder: 'Optional, comma-sep' });
  const { wrap: pSeasonWrap,  input: pSeasonEl  } = mkField({ label: 'Seasonal Priorities', placeholder: 'Summer, Christmas' });
  const { wrap: pRefreshWrap, input: pRefreshEl } = mkField({ label: 'Refresh Interval (mins)', placeholder: '60', type: 'number' });
  const toggleWrap   = el('div', { class: 'i-toggle-wrap' });
  const toggleLabel  = el('label', { class: 'i-toggle' });
  const toggleInput  = el('input', { type: 'checkbox' }); toggleInput.checked = true;
  const toggleSlider = el('span', { class: 'i-toggle-slider' });
  toggleLabel.append(toggleInput, toggleSlider);
  toggleWrap.append(toggleLabel, el('span', { class: 'i-toggle-lbl' }, 'Profile Active'));
  profileGrid.append(pNameWrap, pInduWrap, pAudWrap, pLocWrap, pProdWrap, pWatchWrap, pSeasonWrap, pRefreshWrap, toggleWrap);
  profileBody.appendChild(profileGrid);
  const profileActions = el('div', { class: 'i-filter-row', style: 'margin-top:16px' });
  const saveProfileBtn = el('button', { class: 'i-btn i-btn-primary' }, '💾 Save Profile');
  profileActions.appendChild(saveProfileBtn);
  profileBody.appendChild(profileActions);
  profilePanel.appendChild(profileWrap);

  // ─────────────────────────────────────────────────────────────────────────
  // State helpers
  // ─────────────────────────────────────────────────────────────────────────
  const setLoading = v => { state.loading = v; applyBtn.disabled = v; applyBtn.textContent = v ? '⏳ Loading…' : '⚡ Search Trends'; };
  const setRefreshLoading = v => { state.refreshLoading = v; refreshBtn.disabled = v; refreshBtn.textContent = v ? '⏳…' : '↻ Refresh'; };
  const setProfileLoading = v => { state.profileLoading = v; saveProfileBtn.disabled = v; saveProfileBtn.textContent = v ? '⏳ Saving…' : '💾 Save Profile'; };

  const readFilters = () => ({
    siteId:          siteEl.value,
    keyword:         kwEl.value.trim(),
    category:        catEl.value.trim(),
    location:        locEl.value.trim(),
    timeWindow:      twEl.value,
    audienceType:    audEl.value,
    ageRange:        ageEl.value,
    searchType:      searchEl.value,
    categoryId:      gcatEl.value || undefined,
    subRegion:       srEl.value   || undefined,
    comparisonTerms: compEl.value.trim() || undefined,
  });

  const readProfile = () => ({
    profileName:               pNameEl.value.trim(),
    industryCategory:          pInduEl.value.trim(),
    primaryAudienceType:       pAudEl.value,
    targetLocations:           parseCsv(pLocEl.value),
    primaryProductsOrServices: parseCsv(pProdEl.value),
    watchTopics:               parseCsv(pWatchEl.value),
    seasonalPriorities:        parseCsv(pSeasonEl.value),
    isActive:                  toggleInput.checked,
    refreshIntervalMinutes:    pRefreshEl.value ? Number(pRefreshEl.value) : null,
  });

  const applyProfile = p => {
    pNameEl.value    = p?.profileName || '';
    pInduEl.value    = p?.industryCategory || '';
    pAudEl.value     = p?.primaryAudienceType || '';
    pLocEl.value     = joinList(p?.targetLocations);
    pProdEl.value    = joinList(p?.primaryProductsOrServices);
    pWatchEl.value   = joinList(p?.watchTopics);
    pSeasonEl.value  = joinList(p?.seasonalPriorities);
    pRefreshEl.value = p?.refreshIntervalMinutes ? String(p.refreshIntervalMinutes) : '';
    toggleInput.checked = p?.isActive ?? true;
  };

  const syncSites = () => {
    siteEl.innerHTML = '';
    if (!state.sites.length) { siteEl.appendChild(el('option', { value: '' }, 'No sites yet')); return; }
    state.sites.forEach(s => {
      const id  = s.siteId || s.id;
      const opt = el('option', { value: id }, s.domain || id);
      if (id === state.filters.siteId) opt.selected = true;
      siteEl.appendChild(opt);
    });
  };

  const updateHero = dash => {
    heroTotal.textContent  = dash?.totalItems ?? '—';
    heroAvg.textContent    = dash?.summary?.averageScore != null ? Math.round(dash.summary.averageScore) : '—';
    heroTop.textContent    = dash?.summary?.topQueryOrTopic
      ? dash.summary.topQueryOrTopic.slice(0, 14) + (dash.summary.topQueryOrTopic.length > 14 ? '…' : '') : '—';
    heroRising.textContent = Array.isArray(dash?.risingQueries) ? dash.risingQueries.length : '—';
  };

  const updateMetrics = dash => {
    mTotalVal.textContent  = dash.totalItems ?? 0;
    mAvgVal.textContent    = dash.summary?.averageScore != null ? Math.round(dash.summary.averageScore) : '—';
    mRisingVal.textContent = Array.isArray(dash.risingQueries)  ? dash.risingQueries.length  : 0;
    mRelVal.textContent    = Array.isArray(dash.relatedQueries) ? dash.relatedQueries.length : 0;
  };

  const renderSignalsTable = dash => {
    trendsBody.replaceChildren(buildTable(dash?.topItems));
    trendsBody.style.display = ''; metricsRow.style.display = '';
  };

  const renderRising = dash => {
    const items = Array.isArray(dash?.risingQueries) ? dash.risingQueries : [];
    risingBody.replaceChildren();
    if (!items.length) { risingBody.innerHTML = '<div class="i-empty"><div class="i-empty-icon">🚀</div><div class="i-empty-title">No rising data</div><div class="i-empty-desc">Try a broader keyword or longer time window.</div></div>'; return; }
    const hd = el('div', { class: 'i-section-hd', style: 'padding:14px 18px 0' });
    hd.appendChild(el('span', { class: 'i-section-title' }, `${items.length} rapidly rising searches`));
    hd.appendChild(el('span', { class: 'i-section-meta' }, 'Surging in your market'));
    risingBody.append(hd, buildTable(items.map(i => ({ ...i, isRising: true })), { showRising: true }));
  };

  const renderRelated = dash => {
    const items = Array.isArray(dash?.relatedQueries) ? dash.relatedQueries : [];
    relatedBody.replaceChildren();
    if (!items.length) { relatedBody.innerHTML = '<div class="i-empty"><div class="i-empty-icon">🔗</div><div class="i-empty-title">No related searches</div><div class="i-empty-desc">Related queries appear once Google Trends data is loaded.</div></div>'; return; }
    const hd = el('div', { class: 'i-section-hd', style: 'padding:14px 18px 0' });
    hd.appendChild(el('span', { class: 'i-section-title' }, `${items.length} related search topics`));
    hd.appendChild(el('span', { class: 'i-section-meta' }, 'What else your audience searches'));
    relatedBody.append(hd, buildTable(items));
  };

  const renderOpportunities = dash => {
    const all = [...(dash?.topItems || []), ...(dash?.risingQueries || [])];
    oppsBody.replaceChildren();
    if (!all.length) { oppsBody.innerHTML = '<div class="i-empty"><div class="i-empty-icon">💡</div><div class="i-empty-title">No opportunities yet</div><div class="i-empty-desc">Load trend data from the Signals tab first.</div></div>'; return; }
    const groups = { Transactional: [], Commercial: [], Informational: [] };
    all.forEach(item => { const i = inferIntent(item.queryOrTopic); if (groups[i]) groups[i].push(item); });
    const hd = el('div', { class: 'i-section-hd', style: 'margin-bottom:14px' });
    hd.appendChild(el('span', { class: 'i-section-title' }, `${all.length} opportunity signals`));
    hd.appendChild(el('span', { class: 'i-section-meta' }, fmtTimeAgo(dash.refreshedAtUtc)));
    oppsBody.appendChild(hd);
    const OCFG = {
      Transactional: { title: '🔥 High-Intent Buyers',     desc: 'Ready to purchase or hire. Act now — highest conversion potential.',            action: '→ Run a targeted campaign'   },
      Commercial:    { title: '🎯 Comparison Shoppers',    desc: 'Evaluating options. Ideal for reviews, testimonials, and comparison pages.',    action: '→ Create comparison content'  },
      Informational: { title: '📚 Early-Stage Audience',   desc: 'Building awareness. Perfect for guides, how-to content, and lead magnets.',    action: '→ Publish educational content' },
    };
    const grid = el('div', { class: 'i-opps' });
    Object.entries(groups).forEach(([intent, items]) => {
      if (!items.length) return;
      const cfg = INTENT[intent]; const ocfg = OCFG[intent];
      const top = Math.max(...items.map(i => Number(i.score) || 0));
      const card = el('div', { class: 'i-opp', style: `background:${cfg.bg};border-color:${cfg.border}` });
      const head = el('div', { class: 'i-opp-head' });
      head.append(el('div', { class: 'i-opp-title' }, ocfg.title), el('div', { class: 'i-opp-score', style: `color:${cfg.text}` }, String(Math.round(top))));
      card.appendChild(head);
      card.appendChild(el('div', { class: 'i-opp-desc' }, ocfg.desc));
      const chips = el('div', { class: 'i-opp-chips' });
      items.slice(0, 5).forEach(i => chips.appendChild(el('span', { class: 'i-opp-chip', style: `background:rgba(255,255,255,.65);border-color:${cfg.border};color:${cfg.text}` }, i.queryOrTopic)));
      if (items.length > 5) chips.appendChild(el('span', { style: `font-size:10px;color:${cfg.text};padding:2px 3px` }, `+${items.length - 5} more`));
      card.appendChild(chips);
      card.appendChild(el('div', { class: 'i-opp-action', style: `color:${cfg.text}` }, ocfg.action));
      grid.appendChild(card);
    });
    oppsBody.appendChild(grid);
  };

  // ─────────────────────────────────────────────────────────────────────────
  // API calls
  // ─────────────────────────────────────────────────────────────────────────

  const loadDashboard = async () => {
    state.filters = { ...state.filters, ...readFilters() };
    if (!state.filters.siteId) { notifier.show({ message: 'Please select a site first.', variant: 'warning' }); return; }
    setLoading(true); refreshTxt.textContent = 'Loading…';
    try {
      const dash = await client.intelligence.dashboard({
        siteId:          state.filters.siteId,
        keyword:         state.filters.keyword         || undefined,
        category:        state.filters.category        || undefined,
        location:        state.filters.location        || undefined,
        timeWindow:      state.filters.timeWindow      || undefined,
        audienceType:    state.filters.audienceType    || undefined,
        ageRange:        state.filters.ageRange        || undefined,
        categoryId:      state.filters.categoryId      || undefined,
        searchType:      state.filters.searchType      || undefined,
        comparisonTerms: state.filters.comparisonTerms || undefined,
        subRegion:       state.filters.subRegion       || undefined,
      });
      state.dashboard = dash; state.hasEverLoaded = true; onboard.style.display = 'none';
      updateHero(dash); updateMetrics(dash);
      renderSignalsTable(dash); renderRising(dash); renderRelated(dash); renderOpportunities(dash);
      refreshTxt.textContent = 'Data ready';
      refreshAt.textContent  = ` · ${fmtTimeAgo(dash.refreshedAtUtc) || 'just now'}`;
      aiBox.style.display = ''; aiText.textContent = '…loading insight…';
      client.intelligence.siteSummary({
        siteId: state.filters.siteId, category: dash.category, location: dash.location,
        timeWindow: dash.timeWindow, audienceType: dash.audienceType,
        ageRange: state.filters.ageRange || undefined, searchType: state.filters.searchType || undefined,
      }).then(sum => {
        aiText.textContent = sum.summary || 'No insights available for this segment.';
        aiFooter.textContent = `${sum.usedAi ? '✦ AI Summary' : '⊞ Deterministic'} · ${fmtDate(sum.generatedAtUtc)}`;
      }).catch(() => { aiText.textContent = 'AI insight unavailable right now.'; aiFooter.textContent = ''; });
    } catch (err) {
      const uiErr = mapApiError(err); refreshTxt.textContent = 'Failed';
      if (uiErr.status !== 401 && uiErr.status !== 403) notifier.show({ message: uiErr.message, variant: 'danger' });
    } finally { setLoading(false); }
  };

  const handleRefresh = async () => {
    if (!state.filters.siteId) { notifier.show({ message: 'Select a site first.', variant: 'warning' }); return; }
    setRefreshLoading(true);
    try {
      await client.intelligence.refresh({
        siteId:          state.filters.siteId,
        category:        state.filters.category     || 'general',
        location:        state.filters.location     || 'GB',
        timeWindow:      state.filters.timeWindow   || '7d',
        keyword:         state.filters.keyword      || undefined,
        ageRange:        state.filters.ageRange     || undefined,
        categoryId:      state.filters.categoryId   || undefined,
        searchType:      state.filters.searchType   || undefined,
        comparisonTerms: state.filters.comparisonTerms || undefined,
        subRegion:       state.filters.subRegion    || undefined,
      });
      notifier.show({ message: 'Trend data refreshed from Google.', variant: 'success' });
      await loadDashboard();
    } catch (err) { notifier.show({ message: mapApiError(err).message, variant: 'danger' }); }
    finally { setRefreshLoading(false); }
  };

  const resetFilters = () => {
    const siteId = state.filters.siteId;
    state.filters = { ...DEFAULT_FILTERS, siteId };
    [kwEl, catEl, locEl, compEl].forEach(i => { i.value = ''; });
    [twEl, audEl, ageEl, searchEl, gcatEl, srEl].forEach(s => { s.value = ''; });
    twEl.value = '7d';
  };

  const loadProfile = async () => {
    const siteId = siteEl.value || state.filters.siteId; if (!siteId) return;
    profilePill.className = 'i-pill i-pill-loading'; profilePill.textContent = '⬤ Loading…';
    try {
      const p = await client.intelligence.getProfile(siteId); applyProfile(p);
      if (!catEl.value && p.industryCategory) catEl.value = p.industryCategory;
      if (!locEl.value && p.targetLocations?.length) locEl.value = p.targetLocations[0];
      if (!audEl.value && p.primaryAudienceType) audEl.value = p.primaryAudienceType;
      profilePill.className = 'i-pill i-pill-active';
      profilePill.textContent = p.isActive ? '⬤ Profile active' : '⬤ Profile inactive';
    } catch (err) {
      applyProfile(null); profilePill.className = 'i-pill i-pill-inactive';
      profilePill.textContent = mapApiError(err).status === 404 ? '⬤ No profile yet' : '⬤ Load failed';
    }
  };

  const saveProfile = async () => {
    const siteId = siteEl.value || state.filters.siteId;
    if (!siteId) { notifier.show({ message: 'Select a site first.', variant: 'warning' }); return; }
    setProfileLoading(true);
    try {
      const saved = await client.intelligence.upsertProfile(siteId, readProfile()); applyProfile(saved);
      profilePill.className = 'i-pill i-pill-active';
      profilePill.textContent = saved.isActive ? '⬤ Profile active' : '⬤ Profile inactive';
      notifier.show({ message: 'Profile saved.', variant: 'success' });
    } catch (err) { notifier.show({ message: mapApiError(err).message, variant: 'danger' }); }
    finally { setProfileLoading(false); }
  };

  siteEl.addEventListener('change', async () => { state.filters.siteId = siteEl.value; await loadProfile(); });

  // ── Wire event listeners (after all function declarations) ────────────────
  refreshBtn.addEventListener('click', handleRefresh);
  applyBtn.addEventListener('click', loadDashboard);
  resetBtn.addEventListener('click', resetFilters);
  saveProfileBtn.addEventListener('click', saveProfile);

  // Load network signals when tab is first switched to
  const origSwitchTab = switchTab;
  const switchTabWrapped = key => {
    origSwitchTab(key);
    if (key === 'network' && !nsState.data && !nsState.loading) {
      loadNetworkSignals();
    }
    if (key === 'competitors' && !csState.data && !csState.loading) {
      // Pre-fill industry from profile if available and input is empty
      if (!csState.industry && pInduEl.value) {
        csIndustryEl.value = pInduEl.value;
        csState.industry = pInduEl.value.trim();
      }
    }
    if (key === 'keymap') {
      loadKeymap();
    }
  };
  // Rewire tab buttons to use wrapped switch
  TABS.forEach(({ key }) => {
    const btn = tabEls[key];
    if (btn) {
      const cloned = btn.cloneNode(true);
      cloned.addEventListener('click', () => switchTabWrapped(key));
      btn.replaceWith(cloned);
      tabEls[key] = cloned;
    }
  });

  // Init
  const initialize = async () => {
    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      state.filters.siteId = state.sites[0]?.siteId || state.sites[0]?.id || '';
      syncSites(); if (state.filters.siteId) siteEl.value = state.filters.siteId;
      await loadProfile();
    } catch (err) {
      const uiErr = mapApiError(err);
      if (uiErr.status !== 401 && uiErr.status !== 403) notifier.show({ message: 'Could not load sites.', variant: 'danger' });
    }
  };
  initialize();
};
