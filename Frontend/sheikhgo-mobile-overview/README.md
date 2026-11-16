# SheikhGo Mobile Overview (React Prototype)

Interactive high-density Overview mock for SheikhGo Fleet (Sheikh Travel workspace).

**Stack:** Vite · React 18+ · Tailwind CSS 3 · Lucide icons  
**Purpose:** Visual/interaction sign-off before Flutter production port. Uses demo mock data only.

## Run

```bash
cd Frontend/sheikhgo-mobile-overview
npm install
npm run dev
```

Open the local Vite URL (usually `http://localhost:5173`).

## Screenshot checklist

- [ ] Single notification badge on header bell (none on More tab)
- [ ] Compact 2×2 KPI bento above the fold
- [ ] Fleet Activity segmented bar + legend
- [ ] Regional Punjab map with top-left status pill
- [ ] AI FAB above bottom nav (`bottom-24`)
- [ ] Tabs: Overview · Map · Vehicles · Drivers · More
- [ ] Alert drawer + AI copilot drawer interactive

## Build

```bash
npm run build
npm run preview
```
