import type { Chart, ChartConfiguration, ChartItem, ChartOptions } from 'chart.js';

/** Chart instance type for mixed bar/line/doughnut screens (Chart.js generics are overly strict). */
export type AppChartInstance = Chart<any, any, any>;

/** Chart options type compatible with ui-chart and direct Chart() construction. */
export type AppChartOptions = ChartOptions<any>;

type AppChartConfiguration = ChartConfiguration<any, any, any>;

let chartJsRegistered = false;

/**
 * Creates a Chart.js instance without strict per-chart-type generic checks.
 * Dynamically imports chart.js so feature modules that only call this on view-init
 * do not pull Chart into the shared/eager graph.
 */
export async function createAppChart(
  item: ChartItem,
  config: AppChartConfiguration
): Promise<AppChartInstance> {
  const mod = await import('chart.js');
  if (!chartJsRegistered) {
    mod.Chart.register(...mod.registerables);
    chartJsRegistered = true;
  }
  return new mod.Chart(item, config);
}
