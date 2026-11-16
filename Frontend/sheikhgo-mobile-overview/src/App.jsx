import React, { useState, useMemo } from 'react';
import {
  Wifi,
  Power,
  Pause,
  Play,
  Search,
  SlidersHorizontal,
  Bell,
  Settings,
  ChevronRight,
  ChevronDown,
  Building2,
  Map as MapIcon,
  Car,
  Users,
  LayoutGrid,
  MoreHorizontal,
  KeyRound,
  AlertTriangle,
  Bot,
  X,
  Send,
  Smartphone,
  Maximize2,
  ShieldAlert,
  Sparkles,
  RefreshCw,
  MapPin,
  Check,
} from 'lucide-react';

const COMPANIES = [
  { id: 'sheikh-travel', name: 'Sheikh Travel', fleetSize: 2, activeRegion: 'Punjab, PK' },
  { id: 'sheikh-cargo', name: 'Sheikh Logistics & Cargo', fleetSize: 14, activeRegion: 'GT Road Corridor' },
  { id: 'sheikh-exec', name: 'Sheikh Executive Chauffeurs', fleetSize: 6, activeRegion: 'Lahore / Islamabad' },
];

const INITIAL_VEHICLES = [
  {
    id: 'VEH-101',
    name: 'Toyota Corolla GLi',
    plate: 'LEA-24-8819',
    driver: 'Ali Raza',
    status: 'idle',
    ignition: 'On',
    speed: '0 km/h',
    location: 'Sialkot Bypass, Punjab',
    coords: { x: 58, y: 36 },
    lastSeen: 'Just now',
    fuel: '78%',
    battery: '12.6V',
    routeDeviation: '84.9 km off corridor',
  },
  {
    id: 'VEH-102',
    name: 'Toyota Corolla Altis',
    plate: 'LHR-23-4102',
    driver: 'Usman Tariq',
    status: 'idle',
    ignition: 'Off',
    speed: '0 km/h',
    location: 'Daska - Pasrur Rd, Punjab',
    coords: { x: 46, y: 54 },
    lastSeen: '2m ago',
    fuel: '64%',
    battery: '12.4V',
    routeDeviation: 'On designated hub',
  },
];

const INITIAL_ALERTS = [
  {
    id: 'ALT-1',
    type: 'anomaly',
    severity: 'critical',
    title: 'Route Corridor Deviation',
    subtitle: 'Toyota Corolla GLi (Ali Raza) • ~84.9 km off corridor',
    count: 3,
    time: '34m ago',
    unread: true,
    category: 'Security',
  },
  {
    id: 'ALT-2',
    type: 'ignition_off',
    severity: 'neutral',
    title: 'Ignition Off',
    subtitle: 'Toyota Corolla Altis • Grouped events',
    count: 3,
    time: '6h ago',
    unread: true,
    category: 'Telemetry',
  },
  {
    id: 'ALT-3',
    type: 'ignition_on',
    severity: 'info',
    title: 'Ignition On',
    subtitle: 'Toyota Corolla GLi & Altis • Ali Raza',
    count: 3,
    time: '7h ago',
    unread: false,
    category: 'Telemetry',
  },
  {
    id: 'ALT-4',
    type: 'idle_long',
    severity: 'warning',
    title: 'Extended Engine Idle (> 25m)',
    subtitle: 'Toyota Corolla GLi • Sialkot Bypass',
    count: 1,
    time: '12m ago',
    unread: true,
    category: 'Efficiency',
  },
];

const INITIAL_ACTIVITY = [
  {
    id: 'ACT-1',
    title: 'Fleet anomaly: off route',
    detail: 'Vehicle is ~84.9 km off the Lahore–Sialkot trip corridor.',
    vehicle: 'Toyota Corolla GLi',
    driver: 'Ali Raza',
    occurrences: 3,
    time: '34 min ago',
    severity: 'warning',
    coords: '32.4945° N, 74.5229° E',
  },
  {
    id: 'ACT-2',
    title: 'Geofence Checkpoint Entered',
    detail: 'Arrived at Pasrur Regional Staging Hub.',
    vehicle: 'Toyota Corolla Altis',
    driver: 'Usman Tariq',
    occurrences: 1,
    time: '2 hrs ago',
    severity: 'normal',
    coords: '32.2628° N, 74.6634° E',
  },
  {
    id: 'ACT-3',
    title: 'Shift Telemetry Synced',
    detail: 'OBD-II diagnostics verified healthy across 2 active units.',
    vehicle: 'All Fleet Units',
    driver: 'System Automated',
    occurrences: 1,
    time: '4 hrs ago',
    severity: 'normal',
    coords: 'Punjab Command Server',
  },
];

export default function App() {
  const [mobileFrame, setMobileFrame] = useState(true);
  const [activeTab, setActiveTab] = useState('overview');
  const [selectedCompany, setSelectedCompany] = useState(COMPANIES[0]);
  const [companyDropdownOpen, setCompanyDropdownOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [activeStatusFilter, setActiveStatusFilter] = useState('all');
  const [filterDrawerOpen, setFilterDrawerOpen] = useState(false);
  const [severityFilter, setSeverityFilter] = useState('all');
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [aiDrawerOpen, setAiDrawerOpen] = useState(false);
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [selectedVehiclePin, setSelectedVehiclePin] = useState(INITIAL_VEHICLES[0]);
  const [selectedActivityDetail, setSelectedActivityDetail] = useState(null);
  const [isSyncing, setIsSyncing] = useState(false);
  const [alerts, setAlerts] = useState(INITIAL_ALERTS);
  const [aiMessages, setAiMessages] = useState([
    {
      role: 'assistant',
      text: 'Salam System! Sheikh Travel has 2 vehicles online and currently idle in Punjab. 1 vehicle (Toyota Corolla GLi) triggered an 84.9 km route deviation alert 34m ago. Would you like me to ping driver Ali Raza or review the corridor?',
    },
  ]);
  const [aiInput, setAiInput] = useState('');

  const unreadAlertsCount = useMemo(
    () => alerts.reduce((acc, item) => (item.unread ? acc + item.count : acc), 0),
    [alerts],
  );

  const filteredAlerts = useMemo(() => {
    return alerts.filter((alert) => {
      const matchesQuery =
        alert.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
        alert.subtitle.toLowerCase().includes(searchQuery.toLowerCase());
      const matchesSeverity = severityFilter === 'all' || alert.severity === severityFilter;
      return matchesQuery && matchesSeverity;
    });
  }, [alerts, searchQuery, severityFilter]);

  const filteredActivity = useMemo(() => {
    return INITIAL_ACTIVITY.filter(
      (act) =>
        act.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
        act.detail.toLowerCase().includes(searchQuery.toLowerCase()) ||
        act.vehicle.toLowerCase().includes(searchQuery.toLowerCase()) ||
        act.driver.toLowerCase().includes(searchQuery.toLowerCase()),
    );
  }, [searchQuery]);

  const filteredVehicles = useMemo(() => {
    return INITIAL_VEHICLES.filter((v) => {
      const q = searchQuery.toLowerCase();
      const matchesQ =
        !q ||
        v.name.toLowerCase().includes(q) ||
        v.plate.toLowerCase().includes(q) ||
        v.driver.toLowerCase().includes(q);
      const matchesStatus = activeStatusFilter === 'all' || v.status === activeStatusFilter;
      return matchesQ && matchesStatus;
    });
  }, [searchQuery, activeStatusFilter]);

  const handleManualSync = () => {
    setIsSyncing(true);
    setTimeout(() => setIsSyncing(false), 750);
  };

  const handleMarkAllRead = () => {
    setAlerts((prev) => prev.map((a) => ({ ...a, unread: false })));
  };

  const handleSendAiMessage = (customPrompt) => {
    const textToSend = customPrompt || aiInput.trim();
    if (!textToSend) return;

    const userMsg = { role: 'user', text: textToSend };
    let replyText =
      'Both Toyota Corolla units are reporting healthy battery voltage (12.4V–12.6V) and GPS lock in the Sialkot–Pasrur sector.';

    if (textToSend.toLowerCase().includes('route') || textToSend.toLowerCase().includes('anomaly')) {
      replyText =
        'Toyota Corolla GLi (LEA-24-8819, Driver: Ali Raza) deviated ~84.9 km from the assigned Lahore expressway corridor near Sialkot Bypass. 3 duplicate pings were consolidated into 1 incident.';
    } else if (textToSend.toLowerCase().includes('idle')) {
      replyText =
        '100% of active fleet (2/2 units) is currently Idle: LEA-24-8819 (Ignition On, stationary 14m) and LHR-23-4102 (Ignition Off at Pasrur Hub).';
    }

    setAiMessages((prev) => [...prev, userMsg, { role: 'assistant', text: replyText }]);
    if (!customPrompt) setAiInput('');
  };

  const kpiMetrics = [
    {
      key: 'online',
      label: 'Online',
      value: 2,
      sub: '100% connected',
      icon: Wifi,
      iconBg: 'bg-emerald-500/15 text-emerald-700',
      ringActive: 'ring-2 ring-teal-600 bg-white',
      dot: 'bg-emerald-500',
    },
    {
      key: 'offline',
      label: 'Offline',
      value: 0,
      sub: '0 disconnected',
      icon: Power,
      iconBg: 'bg-rose-500/10 text-rose-600',
      ringActive: 'ring-2 ring-rose-500 bg-white',
      dot: 'bg-rose-400',
    },
    {
      key: 'idle',
      label: 'Idle',
      value: 2,
      sub: 'Stationary units',
      icon: Pause,
      iconBg: 'bg-amber-500/15 text-amber-700',
      ringActive: 'ring-2 ring-amber-500 bg-white',
      dot: 'bg-amber-500',
    },
    {
      key: 'moving',
      label: 'Moving',
      value: 0,
      sub: '0 km/h avg',
      icon: Play,
      iconBg: 'bg-teal-500/15 text-teal-700',
      ringActive: 'ring-2 ring-teal-600 bg-white',
      dot: 'bg-teal-600',
    },
  ];

  return (
    <div className="min-h-screen bg-slate-200/80 text-slate-900 flex flex-col items-center justify-center p-0 sm:p-4 md:p-6 font-sans antialiased selection:bg-teal-700 selection:text-white">
      <div className="hidden sm:flex items-center justify-between w-full max-w-[430px] mb-3 px-2 text-xs text-slate-600">
        <div className="flex items-center gap-1.5 font-medium">
          <span className="w-2 h-2 rounded-full bg-teal-600 animate-pulse" />
          <span>SheikhGo Mobile UI Redesign</span>
        </div>
        <button
          type="button"
          onClick={() => setMobileFrame(!mobileFrame)}
          className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/90 hover:bg-white text-slate-700 shadow-sm border border-slate-200/80 transition cursor-pointer"
        >
          {mobileFrame ? <Maximize2 className="w-3.5 h-3.5" /> : <Smartphone className="w-3.5 h-3.5" />}
          <span>{mobileFrame ? 'Expand Width' : 'Mobile Frame'}</span>
        </button>
      </div>

      <div
        className={`relative bg-[#F1F5F4] text-slate-900 flex flex-col overflow-hidden transition-all duration-300 ${
          mobileFrame
            ? 'w-full sm:w-[412px] h-[100dvh] sm:h-[852px] sm:rounded-[42px] sm:border-[10px] sm:border-slate-900 sm:shadow-2xl'
            : 'w-full max-w-4xl h-[100dvh] sm:h-[860px] sm:rounded-3xl sm:border sm:border-slate-300 sm:shadow-xl'
        }`}
      >
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-12 -right-12 w-56 h-56 rounded-full bg-gradient-to-br from-teal-500/15 via-emerald-400/10 to-transparent blur-xl"
        />

        {mobileFrame && (
          <div className="hidden sm:flex items-center justify-between px-7 pt-3 pb-1 text-[12px] font-semibold text-slate-800 select-none z-20">
            <span>4:36</span>
            <div className="w-24 h-5 bg-slate-900 rounded-full mx-auto" />
            <div className="flex items-center gap-1.5">
              <Wifi className="w-3.5 h-3.5 stroke-[2.5]" />
              <div className="w-5 h-2.5 border border-slate-800 rounded-[3px] p-[1px] flex items-center">
                <div className="w-full h-full bg-slate-800 rounded-[1px]" />
              </div>
            </div>
          </div>
        )}

        <header className="relative z-20 px-4 pt-3 pb-2">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-[#0F766E] to-[#115E59] flex items-center justify-center shadow-sm shadow-teal-900/20 text-white relative overflow-hidden">
                <svg viewBox="0 0 32 32" className="w-6 h-6" fill="none">
                  <circle cx="16" cy="16" r="11" stroke="white" strokeOpacity="0.28" strokeWidth="1.75" />
                  <path d="M9 19.5C11.5 14 16.5 12 23 12.5" stroke="#5EEAD4" strokeWidth="2.2" strokeLinecap="round" />
                  <path d="M11 22.5H21M13.5 17.5H18.5L20 20.5H12L13.5 17.5Z" stroke="white" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <div>
                <div className="flex items-center gap-1.5">
                  <span className="font-bold text-[18px] tracking-tight text-slate-900 leading-none">SheikhGo</span>
                  <span className="text-[10px] font-semibold uppercase tracking-wider px-1.5 py-0.5 rounded bg-teal-700/10 text-teal-800">
                    Fleet
                  </span>
                </div>
                <div className="relative mt-1">
                  <button
                    type="button"
                    onClick={() => setCompanyDropdownOpen(!companyDropdownOpen)}
                    className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-600 hover:text-teal-800 transition cursor-pointer"
                  >
                    <Building2 className="w-3.5 h-3.5 text-teal-700" />
                    <span>{selectedCompany.name}</span>
                    <ChevronDown className={`w-3.5 h-3.5 transition-transform ${companyDropdownOpen ? 'rotate-180' : ''}`} />
                  </button>
                  {companyDropdownOpen && (
                    <div className="absolute left-0 mt-1.5 w-60 rounded-2xl bg-white shadow-xl border border-slate-200/90 p-1.5 z-50">
                      <div className="px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                        Switch Organization
                      </div>
                      {COMPANIES.map((comp) => (
                        <button
                          type="button"
                          key={comp.id}
                          onClick={() => {
                            setSelectedCompany(comp);
                            setCompanyDropdownOpen(false);
                          }}
                          className={`w-full text-left px-2.5 py-2 rounded-xl flex items-center justify-between text-xs transition cursor-pointer ${
                            selectedCompany.id === comp.id
                              ? 'bg-teal-50 text-teal-900 font-semibold'
                              : 'hover:bg-slate-50 text-slate-700'
                          }`}
                        >
                          <div>
                            <div>{comp.name}</div>
                            <div className="text-[10px] text-slate-400 font-normal">
                              {comp.activeRegion} • {comp.fleetSize} units
                            </div>
                          </div>
                          {selectedCompany.id === comp.id && <Check className="w-3.5 h-3.5 text-teal-700" />}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={() => setActiveTab('more')}
                aria-label="Settings"
                className="w-9 h-9 rounded-full bg-white/80 hover:bg-white border border-slate-200/70 flex items-center justify-center text-slate-700 shadow-xs transition cursor-pointer"
              >
                <Settings className="w-[18px] h-[18px]" />
              </button>
              <button
                type="button"
                onClick={() => setNotificationsOpen(true)}
                aria-label="Notifications"
                className="relative w-9 h-9 rounded-full bg-white/80 hover:bg-white border border-slate-200/70 flex items-center justify-center text-slate-700 shadow-xs transition cursor-pointer"
              >
                <Bell className="w-[18px] h-[18px]" />
                {unreadAlertsCount > 0 && (
                  <span className="absolute -top-1 -right-1 min-w-[19px] h-[19px] px-1 rounded-full bg-rose-500 text-white text-[10px] font-bold flex items-center justify-center ring-2 ring-[#F1F5F4]">
                    {unreadAlertsCount > 99 ? '99+' : unreadAlertsCount}
                  </span>
                )}
              </button>
            </div>
          </div>

          <div className="mt-3 flex items-end justify-between gap-2">
            <div>
              <h1 className="text-[20px] font-bold tracking-tight text-slate-900 leading-tight">Good Afternoon, System</h1>
              <p className="text-xs text-slate-500 mt-0.5">Fleet command overview</p>
            </div>
            <button
              type="button"
              onClick={handleManualSync}
              className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-500/10 hover:bg-emerald-500/15 border border-emerald-600/20 text-emerald-800 text-[11px] font-medium transition cursor-pointer shrink-0"
              title="Tap to refresh telemetry"
            >
              <RefreshCw className={`w-3 h-3 text-emerald-600 ${isSyncing ? 'animate-spin' : ''}`} />
              <span>{isSyncing ? 'Syncing...' : 'Synced just now'}</span>
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-y-auto px-4 pb-28 space-y-4 scroll-smooth">
          {activeTab === 'overview' && (
            <>
              <div className="flex items-center gap-2 pt-1">
                <div className="relative flex-1">
                  <Search className="w-4 h-4 text-slate-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search vehicles, drivers, trips..."
                    className="w-full h-10 pl-9 pr-8 rounded-2xl bg-white border border-slate-200/80 text-xs text-slate-800 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-teal-600/30 focus:border-teal-600 shadow-2xs transition"
                  />
                  {searchQuery && (
                    <button
                      type="button"
                      onClick={() => setSearchQuery('')}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 cursor-pointer"
                    >
                      <X className="w-3.5 h-3.5" />
                    </button>
                  )}
                </div>
                <button
                  type="button"
                  onClick={() => setFilterDrawerOpen(!filterDrawerOpen)}
                  aria-label="Filter alerts and vehicles"
                  className={`h-10 px-3 rounded-2xl border flex items-center gap-1.5 text-xs font-medium transition cursor-pointer shadow-2xs ${
                    filterDrawerOpen || severityFilter !== 'all'
                      ? 'bg-[#0F766E] text-white border-[#0F766E]'
                      : 'bg-white text-slate-700 border-slate-200/80 hover:bg-slate-50'
                  }`}
                >
                  <SlidersHorizontal className="w-3.5 h-3.5" />
                </button>
              </div>

              {filterDrawerOpen && (
                <div className="flex items-center gap-1.5 overflow-x-auto py-1 no-scrollbar">
                  {[
                    { id: 'all', label: 'All Events' },
                    { id: 'critical', label: 'Critical (3)' },
                    { id: 'warning', label: 'Warnings (1)' },
                    { id: 'neutral', label: 'Ignition' },
                  ].map((chip) => (
                    <button
                      type="button"
                      key={chip.id}
                      onClick={() => setSeverityFilter(chip.id)}
                      className={`px-3 py-1 rounded-full text-[11px] font-medium whitespace-nowrap transition cursor-pointer ${
                        severityFilter === chip.id
                          ? 'bg-teal-800 text-white'
                          : 'bg-white text-slate-600 border border-slate-200/80 hover:bg-slate-50'
                      }`}
                    >
                      {chip.label}
                    </button>
                  ))}
                </div>
              )}

              <section aria-label="Fleet Status Summary">
                <div className="grid grid-cols-2 gap-2.5">
                  {kpiMetrics.map((item) => {
                    const IconComponent = item.icon;
                    const isSelected = activeStatusFilter === item.key;
                    return (
                      <button
                        type="button"
                        key={item.key}
                        onClick={() => setActiveStatusFilter(isSelected ? 'all' : item.key)}
                        className={`text-left p-3 rounded-2xl bg-white/95 border border-slate-200/70 shadow-2xs hover:shadow-sm transition-all cursor-pointer flex flex-col justify-between ${
                          isSelected ? item.ringActive : ''
                        }`}
                      >
                        <div className="flex items-center justify-between w-full">
                          <div className={`w-8 h-8 rounded-xl flex items-center justify-center ${item.iconBg}`}>
                            <IconComponent className="w-4 h-4 stroke-[2.2]" />
                          </div>
                          <div className="flex items-center gap-1 text-[11px] font-medium text-slate-400">
                            <span className={`w-1.5 h-1.5 rounded-full ${item.dot}`} />
                            <ChevronRight className="w-3.5 h-3.5" />
                          </div>
                        </div>
                        <div className="mt-2.5 flex items-baseline justify-between">
                          <div>
                            <div className="text-[11px] font-medium text-slate-500">{item.label}</div>
                            <div className="text-2xl font-bold text-slate-900 tracking-tight leading-none mt-0.5 tabular-nums">
                              {item.value}
                            </div>
                          </div>
                          <span className="text-[10px] font-medium text-slate-400">{item.sub}</span>
                        </div>
                      </button>
                    );
                  })}
                </div>
              </section>

              <section className="bg-white/95 rounded-2xl p-3.5 border border-slate-200/70 shadow-2xs">
                <div className="flex items-center justify-between mb-2.5">
                  <h2 className="text-[14px] font-bold text-slate-900">Fleet Activity</h2>
                  <button
                    type="button"
                    onClick={() => setReportModalOpen(true)}
                    className="inline-flex items-center gap-0.5 text-xs font-semibold text-[#0F766E] hover:text-[#115E59] cursor-pointer"
                  >
                    <span>View Report</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </button>
                </div>
                <div className="w-full h-2.5 rounded-full bg-slate-100 overflow-hidden flex gap-0.5 mb-3">
                  <div style={{ width: '0%' }} className="bg-teal-600 h-full" title="Moving: 0%" />
                  <div style={{ width: '100%' }} className="bg-amber-400 h-full rounded-full" title="Idle: 100%" />
                  <div style={{ width: '0%' }} className="bg-rose-500 h-full" title="Offline: 0%" />
                </div>
                <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-xs">
                  {[
                    ['Moving', 'bg-teal-600', '0', '0%'],
                    ['Idle', 'bg-amber-400', '2', '100%'],
                    ['Offline', 'bg-rose-500', '0', '0%'],
                    ['Never Seen', 'bg-slate-400', '0', '0%'],
                  ].map(([label, dot, count, pct]) => (
                    <div key={label} className="flex items-center justify-between">
                      <span className="inline-flex items-center gap-1.5 text-slate-600">
                        <span className={`w-2 h-2 rounded-full ${dot}`} />
                        {label}
                      </span>
                      <span className="font-bold text-slate-900 tabular-nums">
                        {count} <span className="font-medium text-slate-500 text-[11px]">({pct})</span>
                      </span>
                    </div>
                  ))}
                </div>
              </section>

              <section className="space-y-2">
                <div className="flex items-center justify-between">
                  <h2 className="text-[14px] font-bold text-slate-900">Live Fleet Map</h2>
                  <button
                    type="button"
                    onClick={() => setActiveTab('map')}
                    className="inline-flex items-center gap-0.5 text-xs font-semibold text-[#0F766E] hover:text-[#115E59] cursor-pointer"
                  >
                    <span>Open Map</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </button>
                </div>
                <div className="relative h-44 rounded-2xl overflow-hidden border border-slate-200/90 bg-[#E6F0EC] shadow-2xs">
                  <svg viewBox="0 0 400 200" className="w-full h-full object-cover" aria-label="Regional Fleet Map of Punjab">
                    <rect width="400" height="200" fill="#E9F2EE" />
                    <path d="M0,35 Q95,20 180,55 T400,40 L400,200 L0,200 Z" fill="#DFECE6" />
                    <path d="M-10,130 C70,115 120,75 195,85 C270,95 320,45 410,30" fill="none" stroke="#99D5E0" strokeWidth="7" strokeLinecap="round" />
                    <path d="M45,195 L165,115 L255,68 L355,15" fill="none" stroke="#FFFFFF" strokeWidth="5" />
                    <path d="M45,195 L165,115 L255,68 L355,15" fill="none" stroke="#CBD5E1" strokeWidth="1.5" strokeDasharray="4 3" />
                    <g fill="#64748B" fontSize="8.5" fontWeight="600" fontFamily="sans-serif">
                      <text x="242" y="54">SIALKOT</text>
                      <text x="195" y="128">PASRUR</text>
                      <text x="135" y="102">DASKA</text>
                      <text x="68" y="176">LAHORE CORRIDOR</text>
                    </g>
                  </svg>
                  <div className="absolute top-2.5 left-2.5 inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/95 backdrop-blur-xs border border-slate-200/80 shadow-xs text-[11px] font-semibold text-slate-700">
                    <span className="w-2 h-2 rounded-full bg-amber-500" />
                    <span>0 vehicles moving • 2 idle</span>
                  </div>
                  {INITIAL_VEHICLES.map((veh) => {
                    const isSelected = selectedVehiclePin?.id === veh.id;
                    return (
                      <button
                        type="button"
                        key={veh.id}
                        onClick={() => setSelectedVehiclePin(veh)}
                        style={{ left: `${veh.coords.x}%`, top: `${veh.coords.y}%` }}
                        className={`-translate-x-1/2 -translate-y-1/2 absolute group cursor-pointer transition-transform ${
                          isSelected ? 'scale-110 z-20' : 'hover:scale-105 z-10'
                        }`}
                        aria-label={`Select ${veh.name}`}
                      >
                        <span className="absolute inset-0 rounded-full bg-amber-400/40 animate-ping" />
                        <div
                          className={`relative w-8 h-8 rounded-full flex items-center justify-center shadow-md border-2 border-white ${
                            isSelected ? 'bg-[#0F766E] text-white' : 'bg-amber-500 text-white'
                          }`}
                        >
                          <Pause className="w-3.5 h-3.5 fill-current" />
                        </div>
                      </button>
                    );
                  })}
                  {selectedVehiclePin && (
                    <div className="absolute bottom-2.5 left-2.5 right-2.5 bg-white/95 backdrop-blur-md rounded-xl px-3 py-2 border border-slate-200/90 shadow-sm flex items-center justify-between">
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5">
                          <span className="text-xs font-bold text-slate-900 truncate">{selectedVehiclePin.name}</span>
                          <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-amber-100 text-amber-800">Idle</span>
                        </div>
                        <div className="text-[11px] text-slate-500 truncate">
                          {selectedVehiclePin.driver} • {selectedVehiclePin.location}
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() => setActiveTab('vehicles')}
                        className="ml-2 px-2.5 py-1 rounded-lg bg-teal-50 hover:bg-teal-100 text-[#0F766E] text-[11px] font-semibold shrink-0 cursor-pointer transition"
                      >
                        Inspect
                      </button>
                    </div>
                  )}
                </div>
              </section>

              <section className="space-y-2">
                <div className="flex items-center justify-between">
                  <h2 className="text-[14px] font-bold text-slate-900">Alerts Requiring Attention</h2>
                  <button
                    type="button"
                    onClick={() => setNotificationsOpen(true)}
                    className="inline-flex items-center gap-0.5 text-xs font-semibold text-[#0F766E] hover:text-[#115E59] cursor-pointer"
                  >
                    <span>View All</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </button>
                </div>
                <div className="bg-white/95 rounded-2xl border border-slate-200/70 shadow-2xs divide-y divide-slate-100 overflow-hidden">
                  {filteredAlerts.slice(0, 3).map((alert) => {
                    const severityStyles = {
                      critical: { bg: 'bg-rose-500/12 text-rose-600', badge: 'bg-rose-100 text-rose-700', icon: ShieldAlert },
                      warning: { bg: 'bg-amber-500/15 text-amber-700', badge: 'bg-amber-100 text-amber-800', icon: AlertTriangle },
                      neutral: { bg: 'bg-indigo-500/10 text-indigo-600', badge: 'bg-slate-100 text-slate-600', icon: KeyRound },
                      info: { bg: 'bg-teal-500/12 text-teal-700', badge: 'bg-teal-50 text-teal-700', icon: KeyRound },
                    }[alert.severity];
                    const AlertIcon = severityStyles.icon;
                    return (
                      <button
                        type="button"
                        key={alert.id}
                        onClick={() => setNotificationsOpen(true)}
                        className="w-full text-left p-3 hover:bg-slate-50/90 transition flex items-center justify-between gap-2.5 cursor-pointer"
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          <div className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 ${severityStyles.bg}`}>
                            <AlertIcon className="w-4 h-4" />
                          </div>
                          <div className="min-w-0">
                            <div className="flex items-center gap-1.5">
                              <span className="text-[13px] font-semibold text-slate-900 truncate">{alert.title}</span>
                              {alert.count > 1 && (
                                <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-md ${severityStyles.badge}`}>
                                  x{alert.count}
                                </span>
                              )}
                            </div>
                            <p className="text-[11px] text-slate-500 truncate mt-0.5">{alert.subtitle}</p>
                          </div>
                        </div>
                        <div className="flex items-center gap-1 text-[11px] text-slate-400 shrink-0">
                          <span>{alert.time}</span>
                          <ChevronRight className="w-3.5 h-3.5" />
                        </div>
                      </button>
                    );
                  })}
                </div>
              </section>

              <section className="space-y-2">
                <div className="flex items-center justify-between">
                  <h2 className="text-[14px] font-bold text-slate-900">Recent Activity</h2>
                  <span className="text-[11px] font-medium text-slate-400">Deduplicated ({filteredActivity.length})</span>
                </div>
                <div className="bg-white/95 rounded-2xl border border-slate-200/70 shadow-2xs divide-y divide-slate-100 overflow-hidden">
                  {filteredActivity.map((item) => (
                    <button
                      type="button"
                      key={item.id}
                      onClick={() => setSelectedActivityDetail(item)}
                      className="w-full text-left p-3 hover:bg-slate-50/90 transition flex items-start justify-between gap-2.5 cursor-pointer"
                    >
                      <div className="flex items-start gap-3 min-w-0">
                        <div
                          className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 mt-0.5 ${
                            item.severity === 'warning' ? 'bg-amber-500/15 text-amber-700' : 'bg-teal-500/12 text-teal-700'
                          }`}
                        >
                          <MapPin className="w-4 h-4" />
                        </div>
                        <div className="min-w-0">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <span className="text-[13px] font-semibold text-slate-900">{item.title}</span>
                            {item.occurrences > 1 && (
                              <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-amber-100 text-amber-800">
                                {item.occurrences} grouped events
                              </span>
                            )}
                          </div>
                          <p className="text-[11px] text-slate-500 line-clamp-1 mt-0.5">{item.detail}</p>
                          <div className="flex items-center gap-2 mt-1 text-[10px] font-medium text-slate-400">
                            <span className="text-teal-700 font-semibold">{item.vehicle}</span>
                            <span>•</span>
                            <span>{item.driver}</span>
                          </div>
                        </div>
                      </div>
                      <span className="text-[11px] text-slate-400 shrink-0 whitespace-nowrap">{item.time}</span>
                    </button>
                  ))}
                </div>
              </section>
            </>
          )}

          {activeTab === 'map' && (
            <div className="space-y-3 pt-1">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-base font-bold text-slate-900">Live Regional Telemetry</h2>
                  <p className="text-xs text-slate-500">Punjab Corridor • Auto-refreshing 10s</p>
                </div>
                <span className="px-2.5 py-1 rounded-full bg-teal-700/10 text-teal-800 text-xs font-semibold">2 Units Tracked</span>
              </div>
              <div className="relative h-80 rounded-2xl overflow-hidden border border-slate-200 bg-[#E6F0EC] shadow-sm">
                <svg viewBox="0 0 400 300" className="w-full h-full object-cover">
                  <rect width="400" height="300" fill="#E9F2EE" />
                  <path d="M0,50 Q130,25 220,85 T400,60 L400,300 L0,300 Z" fill="#DFECE6" />
                  <path d="M35,290 L170,175 L270,105 L370,25" fill="none" stroke="#FFFFFF" strokeWidth="6" />
                </svg>
                {INITIAL_VEHICLES.map((v) => (
                  <button
                    type="button"
                    key={v.id}
                    onClick={() => setSelectedVehiclePin(v)}
                    style={{ left: `${v.coords.x}%`, top: `${v.coords.y}%` }}
                    className="-translate-x-1/2 -translate-y-1/2 absolute cursor-pointer"
                  >
                    <div className="px-2.5 py-1 rounded-full bg-slate-900 text-white text-[10px] font-bold shadow-lg flex items-center gap-1.5 border border-white">
                      <span className="w-2 h-2 rounded-full bg-amber-400" />
                      <span>{v.plate}</span>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          {activeTab === 'vehicles' && (
            <div className="space-y-3 pt-1">
              <div className="flex items-center justify-between">
                <h2 className="text-base font-bold text-slate-900">Active Fleet ({filteredVehicles.length})</h2>
                <span className="text-xs text-teal-700 font-semibold">Filter: {activeStatusFilter}</span>
              </div>
              {filteredVehicles.map((veh) => (
                <div key={veh.id} className="p-4 rounded-2xl bg-white border border-slate-200/80 shadow-2xs space-y-3">
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-teal-700/10 text-teal-800 flex items-center justify-center">
                        <Car className="w-5 h-5" />
                      </div>
                      <div>
                        <h3 className="font-bold text-sm text-slate-900">{veh.name}</h3>
                        <p className="text-xs text-slate-500 font-mono">{veh.plate}</p>
                      </div>
                    </div>
                    <span className="px-2.5 py-1 rounded-full bg-amber-500/15 text-amber-800 text-xs font-semibold">Idle</span>
                  </div>
                  <div className="grid grid-cols-3 gap-2 pt-2 border-t border-slate-100 text-xs">
                    <div className="bg-slate-50 p-2 rounded-xl">
                      <span className="text-slate-400 block text-[10px]">Ignition</span>
                      <span className="font-bold text-slate-800">{veh.ignition}</span>
                    </div>
                    <div className="bg-slate-50 p-2 rounded-xl">
                      <span className="text-slate-400 block text-[10px]">Fuel</span>
                      <span className="font-bold text-slate-800">{veh.fuel}</span>
                    </div>
                    <div className="bg-slate-50 p-2 rounded-xl">
                      <span className="text-slate-400 block text-[10px]">Battery</span>
                      <span className="font-bold text-slate-800">{veh.battery}</span>
                    </div>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-500">
                    <span>
                      Driver: <strong className="text-slate-800">{veh.driver}</strong>
                    </span>
                    <span>{veh.location}</span>
                  </div>
                </div>
              ))}
            </div>
          )}

          {activeTab === 'drivers' && (
            <div className="space-y-3 pt-1">
              <h2 className="text-base font-bold text-slate-900">Assigned Drivers (2)</h2>
              {INITIAL_VEHICLES.map((veh, idx) => (
                <div key={veh.id} className="p-4 rounded-2xl bg-white border border-slate-200/80 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-gradient-to-br from-teal-700 to-emerald-600 text-white font-bold text-sm flex items-center justify-center">
                      {veh.driver
                        .split(' ')
                        .map((n) => n[0])
                        .join('')}
                    </div>
                    <div>
                      <div className="font-bold text-sm text-slate-900">{veh.driver}</div>
                      <div className="text-xs text-slate-500">
                        Assigned: {veh.name} ({veh.plate})
                      </div>
                      <div className="text-[11px] text-teal-700 font-medium mt-0.5">
                        Safety Score: {idx === 0 ? '88/100 (1 Route Alert)' : '98/100 (Optimal)'}
                      </div>
                    </div>
                  </div>
                  <ChevronRight className="w-4 h-4 text-slate-400" />
                </div>
              ))}
            </div>
          )}

          {activeTab === 'more' && (
            <div className="space-y-3 pt-1">
              <h2 className="text-base font-bold text-slate-900">Command Center & Preferences</h2>
              <div className="bg-white rounded-2xl border border-slate-200/80 divide-y divide-slate-100 overflow-hidden text-sm">
                {[
                  { label: 'Geofence & Corridor Rules', desc: 'Manage Lahore–Sialkot trip boundaries' },
                  { label: 'Alert Deduplication Engine', desc: 'Group repeated pings within 15 mins' },
                  { label: 'Fleet Utilization Reports', desc: 'Export CSV / PDF telemetry summaries' },
                  { label: 'Organization & API Keys', desc: 'Sheikh Travel (Enterprise Tier)' },
                ].map((row) => (
                  <button
                    type="button"
                    key={row.label}
                    onClick={() => setReportModalOpen(true)}
                    className="w-full text-left p-3.5 hover:bg-slate-50 flex items-center justify-between cursor-pointer"
                  >
                    <div>
                      <div className="font-semibold text-slate-900 text-xs">{row.label}</div>
                      <div className="text-[11px] text-slate-500">{row.desc}</div>
                    </div>
                    <ChevronRight className="w-4 h-4 text-slate-400" />
                  </button>
                ))}
              </div>
            </div>
          )}
        </main>

        <button
          type="button"
          onClick={() => setAiDrawerOpen(true)}
          aria-label="Open SheikhGo AI Assistant"
          className="absolute right-4 bottom-24 z-30 w-12 h-12 rounded-2xl bg-gradient-to-br from-[#0F766E] to-[#115E59] hover:from-[#115E59] hover:to-[#0F766E] text-white shadow-lg shadow-teal-950/25 flex items-center justify-center transition-transform hover:scale-105 active:scale-95 cursor-pointer border border-teal-400/30"
        >
          <Bot className="w-6 h-6" />
          <span className="absolute -top-1 -right-1 w-3 h-3 rounded-full bg-emerald-400 border-2 border-[#F1F5F4]" />
        </button>

        <nav aria-label="Primary Bottom Navigation" className="relative z-30 bg-white/95 backdrop-blur-md border-t border-slate-200/90 px-2 pt-2 pb-4 sm:pb-5">
          <div className="grid grid-cols-5 items-center">
            {[
              { id: 'overview', label: 'Overview', icon: LayoutGrid },
              { id: 'map', label: 'Map', icon: MapIcon },
              { id: 'vehicles', label: 'Vehicles', icon: Car },
              { id: 'drivers', label: 'Drivers', icon: Users },
              { id: 'more', label: 'More', icon: MoreHorizontal },
            ].map((tab) => {
              const Icon = tab.icon;
              const isActive = activeTab === tab.id;
              return (
                <button type="button" key={tab.id} onClick={() => setActiveTab(tab.id)} className="flex flex-col items-center justify-center gap-1 py-1 cursor-pointer group">
                  <div
                    className={`px-3.5 py-1 rounded-xl transition-all ${
                      isActive ? 'bg-[#0F766E] text-white shadow-xs' : 'text-slate-400 group-hover:text-slate-700'
                    }`}
                  >
                    <Icon className="w-4 h-4 stroke-[2.2]" />
                  </div>
                  <span className={`text-[10px] font-semibold tracking-tight ${isActive ? 'text-[#0F766E]' : 'text-slate-500'}`}>
                    {tab.label}
                  </span>
                </button>
              );
            })}
          </div>
        </nav>

        {notificationsOpen && (
          <div className="absolute inset-0 z-50 bg-slate-900/40 backdrop-blur-xs flex flex-col justify-end">
            <div className="bg-white rounded-t-3xl max-h-[82%] flex flex-col shadow-2xl">
              <div className="p-4 border-b border-slate-100 flex items-center justify-between">
                <div>
                  <h3 className="font-bold text-base text-slate-900">Alert Center</h3>
                  <p className="text-xs text-slate-500">Smart-grouped to reduce repetitive telemetry noise</p>
                </div>
                <div className="flex items-center gap-2">
                  <button type="button" onClick={handleMarkAllRead} className="text-xs font-semibold text-[#0F766E] hover:underline cursor-pointer">
                    Mark read
                  </button>
                  <button
                    type="button"
                    onClick={() => setNotificationsOpen(false)}
                    className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center text-slate-600 cursor-pointer"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
              </div>
              <div className="p-4 overflow-y-auto space-y-2.5">
                {alerts.map((al) => (
                  <div
                    key={al.id}
                    className={`p-3 rounded-2xl border ${
                      al.unread ? 'bg-teal-50/40 border-teal-600/25' : 'bg-slate-50/70 border-slate-200/70'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-[10px] font-bold uppercase tracking-wider text-teal-800">{al.category}</span>
                      <span className="text-[11px] text-slate-400">{al.time}</span>
                    </div>
                    <div className="font-bold text-sm text-slate-900 mt-0.5 flex items-center gap-2">
                      <span>{al.title}</span>
                      <span className="text-[11px] px-1.5 py-0.5 rounded bg-slate-200/80 text-slate-700">x{al.count}</span>
                    </div>
                    <p className="text-xs text-slate-600 mt-1">{al.subtitle}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {aiDrawerOpen && (
          <div className="absolute inset-0 z-50 bg-slate-900/40 backdrop-blur-xs flex flex-col justify-end">
            <div className="bg-white rounded-t-3xl h-[78%] flex flex-col shadow-2xl">
              <div className="p-4 bg-gradient-to-r from-[#0F766E] to-[#115E59] text-white rounded-t-3xl flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-xl bg-white/15 flex items-center justify-center">
                    <Sparkles className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="font-bold text-sm">SheikhGo Fleet Copilot</h3>
                    <p className="text-[11px] text-teal-100">Live Telemetry & Route Intelligence</p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setAiDrawerOpen(false)}
                  className="w-8 h-8 rounded-full bg-white/15 hover:bg-white/25 flex items-center justify-center cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
              <div className="flex-1 p-4 overflow-y-auto space-y-3 bg-slate-50">
                {aiMessages.map((msg, i) => (
                  <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                    <div
                      className={`max-w-[85%] rounded-2xl px-3.5 py-2.5 text-xs leading-relaxed ${
                        msg.role === 'user'
                          ? 'bg-[#0F766E] text-white rounded-br-sm'
                          : 'bg-white border border-slate-200/80 text-slate-800 rounded-bl-sm shadow-2xs'
                      }`}
                    >
                      {msg.text}
                    </div>
                  </div>
                ))}
              </div>
              <div className="px-3 pt-2 pb-1 bg-white border-t border-slate-100 flex gap-1.5 overflow-x-auto">
                {['Explain 84.9 km route anomaly', 'Why are both vehicles idle?'].map((prompt) => (
                  <button
                    type="button"
                    key={prompt}
                    onClick={() => handleSendAiMessage(prompt)}
                    className="px-2.5 py-1 rounded-full bg-teal-50 hover:bg-teal-100 text-[#0F766E] text-[11px] font-medium whitespace-nowrap cursor-pointer"
                  >
                    {prompt}
                  </button>
                ))}
              </div>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  handleSendAiMessage();
                }}
                className="p-3 bg-white flex items-center gap-2"
              >
                <input
                  type="text"
                  value={aiInput}
                  onChange={(e) => setAiInput(e.target.value)}
                  placeholder="Ask about vehicles, routes, or alerts..."
                  className="flex-1 h-10 px-3.5 rounded-xl bg-slate-100 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-teal-600"
                />
                <button type="submit" className="w-10 h-10 rounded-xl bg-[#0F766E] text-white flex items-center justify-center hover:bg-[#115E59] cursor-pointer">
                  <Send className="w-4 h-4" />
                </button>
              </form>
            </div>
          </div>
        )}

        {selectedActivityDetail && (
          <div className="absolute inset-0 z-50 bg-slate-900/40 backdrop-blur-xs flex items-center justify-center p-4">
            <div className="bg-white rounded-3xl w-full max-w-sm p-5 shadow-2xl space-y-3">
              <div className="flex items-center justify-between">
                <span className="px-2.5 py-0.5 rounded-full bg-amber-100 text-amber-800 text-[11px] font-bold">Incident Breakdown</span>
                <button
                  type="button"
                  onClick={() => setSelectedActivityDetail(null)}
                  className="w-7 h-7 rounded-full bg-slate-100 flex items-center justify-center text-slate-600 cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
              <h3 className="font-bold text-base text-slate-900">{selectedActivityDetail.title}</h3>
              <p className="text-xs text-slate-600 leading-relaxed">{selectedActivityDetail.detail}</p>
              <button
                type="button"
                onClick={() => {
                  setSelectedActivityDetail(null);
                  setActiveTab('map');
                }}
                className="w-full py-2.5 rounded-xl bg-[#0F766E] text-white text-xs font-semibold hover:bg-[#115E59] cursor-pointer"
              >
                Locate on Live Map
              </button>
            </div>
          </div>
        )}

        {reportModalOpen && (
          <div className="absolute inset-0 z-50 bg-slate-900/40 backdrop-blur-xs flex items-center justify-center p-4">
            <div className="bg-white rounded-3xl w-full max-w-sm p-5 shadow-2xl space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="font-bold text-base text-slate-900">Sheikh Travel • Daily Report</h3>
                <button
                  type="button"
                  onClick={() => setReportModalOpen(false)}
                  className="w-7 h-7 rounded-full bg-slate-100 flex items-center justify-center text-slate-600 cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
              <div className="grid grid-cols-2 gap-2 text-xs">
                <div className="p-3 rounded-2xl bg-teal-50/70 border border-teal-600/15">
                  <span className="text-slate-500 block">Fleet Availability</span>
                  <span className="text-lg font-bold text-teal-900 tabular-nums">100%</span>
                </div>
                <div className="p-3 rounded-2xl bg-amber-50/80 border border-amber-500/20">
                  <span className="text-slate-500 block">Idle Ratio</span>
                  <span className="text-lg font-bold text-amber-800 tabular-nums">100% (2/2)</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setReportModalOpen(false)}
                className="w-full py-2.5 rounded-xl bg-slate-900 text-white text-xs font-semibold cursor-pointer"
              >
                Done
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
