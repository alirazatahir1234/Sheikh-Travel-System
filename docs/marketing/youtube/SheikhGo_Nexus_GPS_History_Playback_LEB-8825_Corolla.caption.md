# SheikhGo Nexus — GPS history playback (ready video)

**File:** `SheikhGo_Nexus_GPS_History_Playback_LEB-8825_Corolla.mp4`  
**Length:** ~25 s · **1920×1080** · logo overlay + Mixkit BGM  
**Not published** (local marketing asset only)

## Vehicle match

| Requested | Result |
|-----------|--------|
| BW2217 | **Not found** |
| BWY 2217 | **Not found** |
| BWY 217 | Found — **Suzuki Every / Ali**, vehicleId **2005** — parked-only (408 pts, 0 km move) |
| Toyota Corolla | Found — plate **LEB-8825**, vehicleId **2** — **used for this video** |

## Data used

- **Source:** prod `GpsPositions` (SheikhGo MCP / live DB)
- **Day:** 2026-09-25
- **Raw points that day:** 5,793
- **Distinct waypoints animated:** 36
- **Odometer delta:** ~146 km · path ≈ 118 km · max speed 73 km/h
- **Time window:** 00:46–18:08 UTC

## On-screen / narration style (Nexus demo)

1. **Title:** SheikhGo NEXUS — *One workspace for live GPS, fleet ops, and history playback*
2. **Vehicle:** Toyota Corolla · LEB-8825 · GPS history replay
3. **Playback captions:** Live GPS history playback → Replay the route — every kilometer → Speed, path, and location in one view → Built for dispatch and fleet control
4. **Stats card:** date, distance, max speed, track points
5. **Close:** Replay routes. Control the fleet. · sheikhgo.com · Book a Demo

## Regenerate

```bash
cd docs/marketing/youtube
python3 render_corolla_history_playback.py
```
