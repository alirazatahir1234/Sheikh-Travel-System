import { Chart, ChartConfiguration, ChartItem, ChartOptions } from 'chart.js';

/** Chart instance type for mixed bar/line/doughnut screens (Chart.js generics are overly strict). */
export type AppChartInstance = Chart<any, any, any>;

/** Chart options type compatible with ui-chart and direct Chart() construction. */
export type AppChartOptions = ChartOptions<any>;

type AppChartConfiguration = ChartConfiguration<any, any, any>;

/** Creates a Chart.js instance without strict per-chart-type generic checks. */
export function createAppChart(item: ChartItem, config: AppChartConfiguration): AppChartInstance {
  return new Chart(item, config);
}
