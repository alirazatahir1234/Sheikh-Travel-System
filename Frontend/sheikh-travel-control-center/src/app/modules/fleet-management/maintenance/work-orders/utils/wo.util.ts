import { WorkOrderListItem } from '../../../../../core/models/maintenance.model';

export function woEstimatedCost(wo: WorkOrderListItem): number {
  return (wo.estimatedLaborCost ?? wo.laborCost ?? 0) + (wo.estimatedPartsCost ?? wo.partsCost ?? 0);
}

export function woActualCost(wo: WorkOrderListItem): number {
  return wo.totalCost ?? (wo.laborCost + wo.partsCost);
}

export function parseServiceItems(serviceTypeName?: string | null): string[] {
  if (!serviceTypeName?.trim()) return [];
  return serviceTypeName.split(',').map(s => s.trim()).filter(Boolean);
}

export function workflowStepIndex(status: string): number {
  const map: Record<string, number> = {
    Draft: 0, Open: 1, Assigned: 2, InProgress: 3, WaitingParts: 3, Completed: 4, Closed: 5
  };
  return map[status] ?? 0;
}
