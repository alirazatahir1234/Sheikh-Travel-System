import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  Maintenance,
  CreateMaintenanceRequest,
  UpdateMaintenanceStatusRequest,
  MaintenanceStatus,
  MaintenanceDashboard,
  MaintenanceAlert,
  MaintenanceRequest,
  CreateMaintenanceRequestPayload,
  WorkOrderListItem,
  WorkOrderDetail,
  WorkOrderStats,
  TechnicianListItem,
  CreateWorkOrderPayload,
  UpdateWorkOrderPayload,
  Workshop,
  CreateWorkshopPayload,
  UpdateWorkshopPayload,
  Vendor,
  CreateVendorPayload,
  UpdateVendorPayload,
  WorkshopVendorStats,
  ServiceType,
  MaintenanceSchedule,
  MaintenanceScheduleListItem,
  MaintenanceScheduleCalendarItem,
  MaintenanceScheduleTemplate,
  CreateMaintenanceSchedulePayload,
  RescheduleMaintenanceSchedulePayload,
  VehicleServiceHistoryItem,
  Part,
  PartsInventoryStats,
  CreatePartPayload,
  AddPartStockPayload,
  IssuePartPayload,
  TransferPartStockPayload,
  MaintenanceReport,
  MaintenanceReportFilters,
  MaintenanceReportSchedule,
  CreateMaintenanceReportSchedulePayload,
  MaintenanceRequestStats,
  MaintenanceSearchResult,
  ComplianceSummary
} from '../models/maintenance.model';

@Injectable({ providedIn: 'root' })
export class MaintenanceService {
  private readonly base = `${environment.apiUrl}/maintenance`;
  private readonly workOrdersBase = `${environment.apiUrl}/workorders`;
  private readonly workshopsBase = `${environment.apiUrl}/workshops`;
  private readonly vendorsBase = `${environment.apiUrl}/vendors`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 20): Observable<PagedResult<Maintenance>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Maintenance>>(this.base, { params });
  }

  getById(id: number): Observable<Maintenance> {
    return this.http.get<Maintenance>(`${this.base}/${id}`);
  }

  create(request: CreateMaintenanceRequest): Observable<number> {
    return this.http.post<number>(this.base, request);
  }

  update(id: number, request: CreateMaintenanceRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${id}`, request);
  }

  updateStatus(request: UpdateMaintenanceStatusRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/${request.id}/status`, { status: request.status });
  }

  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/${id}`);
  }

  // ── Dashboard & module APIs ───────────────────────────────────────────────

  getDashboard(period = 'Month', granularity = 'Day', from?: string, to?: string): Observable<MaintenanceDashboard> {
    let params = new HttpParams().set('period', period).set('granularity', granularity);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<MaintenanceDashboard>(`${this.base}/dashboard`, { params });
  }

  getAlerts(limit = 20): Observable<MaintenanceAlert[]> {
    return this.http.get<MaintenanceAlert[]>(`${this.base}/alerts`, { params: { limit } });
  }

  getRequests(page = 1, pageSize = 20, status?: string, search?: string, priority?: string): Observable<PagedResult<MaintenanceRequest>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (search) params = params.set('search', search);
    if (priority) params = params.set('priority', priority);
    return this.http.get<PagedResult<MaintenanceRequest>>(`${this.base}/requests`, { params });
  }

  getRequestById(id: number): Observable<MaintenanceRequest> {
    return this.http.get<MaintenanceRequest>(`${this.base}/requests/${id}`);
  }

  getRequestStats(): Observable<MaintenanceRequestStats> {
    return this.http.get<MaintenanceRequestStats>(`${this.base}/requests/stats`);
  }

  approveRequest(id: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/requests/${id}/approve`, {});
  }

  rejectRequest(id: number, reason: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/requests/${id}/reject`, { reason });
  }

  uploadRequestAttachment(id: number, file: File): Observable<boolean> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<boolean>(`${this.base}/requests/${id}/attachments`, form);
  }

  search(q: string): Observable<MaintenanceSearchResult[]> {
    return this.http.get<MaintenanceSearchResult[]>(`${this.base}/search`, { params: { q } });
  }

  dismissAlert(id: number): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/alerts/${id}/dismiss`, {});
  }

  getComplianceSummary(): Observable<ComplianceSummary> {
    return this.http.get<ComplianceSummary>(`${this.base}/compliance-summary`);
  }

  createRequest(body: CreateMaintenanceRequestPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/requests`, body);
  }

  convertRequest(id: number, body: Record<string, unknown> = {}): Observable<number> {
    return this.http.post<number>(`${this.base}/requests/${id}/convert`, body);
  }

  getWorkOrders(
    page = 1,
    pageSize = 20,
    filters?: {
      status?: string;
      statuses?: string;
      search?: string;
      vehicleId?: number;
      workshopId?: number;
      priority?: string;
    }
  ): Observable<PagedResult<WorkOrderListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters?.statuses) params = params.set('statuses', filters.statuses);
    else if (filters?.status) params = params.set('status', filters.status);
    if (filters?.search) params = params.set('search', filters.search);
    if (filters?.vehicleId) params = params.set('vehicleId', filters.vehicleId);
    if (filters?.workshopId) params = params.set('workshopId', filters.workshopId);
    if (filters?.priority) params = params.set('priority', filters.priority);
    return this.http.get<PagedResult<WorkOrderListItem>>(this.workOrdersBase, { params });
  }

  getWorkOrderStats(): Observable<WorkOrderStats> {
    return this.http.get<WorkOrderStats>(`${this.workOrdersBase}/stats`);
  }

  getTechnicians(workshopId?: number): Observable<TechnicianListItem[]> {
    let params = new HttpParams();
    if (workshopId) params = params.set('workshopId', workshopId);
    return this.http.get<TechnicianListItem[]>(`${this.workOrdersBase}/technicians`, { params });
  }

  computeWorkOrderStats(items: WorkOrderListItem[]): WorkOrderStats {
    return {
      open: items.filter(o => ['Draft', 'Open', 'Assigned'].includes(o.status)).length,
      inProgress: items.filter(o => ['InProgress', 'WaitingParts'].includes(o.status)).length,
      completed: items.filter(o => ['Completed', 'Closed'].includes(o.status)).length,
      cancelled: items.filter(o => o.status === 'Cancelled').length,
    };
  }

  getWorkOrderById(id: number): Observable<WorkOrderDetail> {
    return this.http.get<WorkOrderDetail>(`${this.workOrdersBase}/${id}`);
  }

  createWorkOrder(body: CreateWorkOrderPayload): Observable<number> {
    return this.http.post<number>(this.workOrdersBase, sanitizeCreateWorkOrderPayload(body));
  }

  updateWorkOrderStatus(id: number, status: string, technicianNotes?: string): Observable<boolean> {
    return this.http.put<boolean>(`${this.workOrdersBase}/${id}/status`, { status, technicianNotes });
  }

  updateWorkOrder(id: number, body: UpdateWorkOrderPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.workOrdersBase}/${id}`, body);
  }

  recordPartUsage(workOrderId: number, partId: number, quantity: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.workOrdersBase}/${workOrderId}/parts`, { partId, quantity });
  }

  getWorkshops(): Observable<Workshop[]> {
    return this.http.get<Workshop[]>(this.workshopsBase);
  }

  getWorkshopById(id: number): Observable<Workshop> {
    return this.http.get<Workshop>(`${this.workshopsBase}/${id}`);
  }

  createWorkshop(body: CreateWorkshopPayload): Observable<number> {
    return this.http.post<number>(this.workshopsBase, body);
  }

  updateWorkshop(id: number, body: UpdateWorkshopPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.workshopsBase}/${id}`, body);
  }

  activateWorkshop(id: number): Observable<boolean> {
    return this.http.put<boolean>(`${this.workshopsBase}/${id}/activate`, {});
  }

  deactivateWorkshop(id: number): Observable<boolean> {
    return this.http.put<boolean>(`${this.workshopsBase}/${id}/deactivate`, {});
  }

  getVendors(): Observable<Vendor[]> {
    return this.http.get<Vendor[]>(this.vendorsBase);
  }

  getVendorById(id: number): Observable<Vendor> {
    return this.http.get<Vendor>(`${this.vendorsBase}/${id}`);
  }

  createVendor(body: CreateVendorPayload): Observable<number> {
    return this.http.post<number>(this.vendorsBase, body);
  }

  updateVendor(id: number, body: UpdateVendorPayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.vendorsBase}/${id}`, body);
  }

  activateVendor(id: number): Observable<boolean> {
    return this.http.put<boolean>(`${this.vendorsBase}/${id}/activate`, {});
  }

  deactivateVendor(id: number): Observable<boolean> {
    return this.http.put<boolean>(`${this.vendorsBase}/${id}/deactivate`, {});
  }

  getWorkshopVendorStats(): Observable<WorkshopVendorStats> {
    return this.http.get<WorkshopVendorStats>(`${this.base}/workshops-vendors/stats`);
  }

  getServiceTypes(): Observable<ServiceType[]> {
    return this.http.get<ServiceType[]>(`${this.base}/service-types`);
  }

  getSchedules(vehicleId?: number): Observable<MaintenanceSchedule[]> {
    let params = new HttpParams();
    if (vehicleId) params = params.set('vehicleId', vehicleId);
    return this.http.get<MaintenanceSchedule[]>(`${this.base}/schedules`, { params });
  }

  getSchedulesEnriched(filters?: {
    vehicleId?: number;
    status?: string;
    search?: string;
  }): Observable<MaintenanceScheduleListItem[]> {
    let params = new HttpParams();
    if (filters?.vehicleId) params = params.set('vehicleId', filters.vehicleId);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.search) params = params.set('search', filters.search);
    return this.http.get<MaintenanceScheduleListItem[]>(`${this.base}/schedules`, { params });
  }

  getScheduleCalendar(from: string, to: string): Observable<MaintenanceScheduleCalendarItem[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<MaintenanceScheduleCalendarItem[]>(`${this.base}/schedules/calendar`, { params });
  }

  getScheduleTemplates(): Observable<MaintenanceScheduleTemplate[]> {
    return this.http.get<MaintenanceScheduleTemplate[]>(`${this.base}/schedules/templates`);
  }

  rescheduleSchedule(id: number, body: RescheduleMaintenanceSchedulePayload): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/schedules/${id}/reschedule`, body);
  }

  updateSchedule(id: number, body: Record<string, unknown>): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/schedules/${id}`, body);
  }

  createWorkOrderFromSchedule(id: number): Observable<number> {
    return this.http.post<number>(`${this.base}/schedules/${id}/work-order`, {});
  }

  createSchedule(body: CreateMaintenanceSchedulePayload | Record<string, unknown>): Observable<number> {
    return this.http.post<number>(`${this.base}/schedules`, body);
  }

  getParts(search?: string): Observable<Part[]> {
    let params = new HttpParams();
    if (search?.trim()) params = params.set('search', search.trim());
    return this.http.get<Part[]>(`${this.base}/parts`, { params });
  }

  getPartsInventoryStats(): Observable<PartsInventoryStats> {
    return this.http.get<PartsInventoryStats>(`${this.base}/parts/stats`);
  }

  createPart(body: CreatePartPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/parts`, body);
  }

  addPartStock(id: number, body: AddPartStockPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/parts/${id}/add-stock`, body);
  }

  issuePart(id: number, body: IssuePartPayload): Observable<number> {
    return this.http.post<number>(`${this.base}/parts/${id}/issue`, body);
  }

  transferPartStock(id: number, body: TransferPartStockPayload): Observable<boolean> {
    return this.http.post<boolean>(`${this.base}/parts/${id}/transfer`, body);
  }

  getReport(reportType: string, filters?: MaintenanceReportFilters): Observable<MaintenanceReport> {
    let params = new HttpParams().set('reportType', reportType);
    if (filters?.from) params = params.set('from', filters.from);
    if (filters?.to) params = params.set('to', filters.to);
    if (filters?.vehicleId) params = params.set('vehicleId', filters.vehicleId);
    if (filters?.branchId) params = params.set('branchId', filters.branchId);
    if (filters?.status) params = params.set('status', filters.status);
    return this.http.get<MaintenanceReport>(`${this.base}/reports`, { params });
  }

  getReportSchedules(): Observable<MaintenanceReportSchedule[]> {
    return this.http.get<MaintenanceReportSchedule[]>(`${this.base}/reports/schedules`);
  }

  createReportSchedule(body: CreateMaintenanceReportSchedulePayload): Observable<number> {
    return this.http.post<number>(`${this.base}/reports/schedules`, body);
  }

  updateReportSchedule(id: number, body: Partial<CreateMaintenanceReportSchedulePayload> & { isActive?: boolean }): Observable<boolean> {
    return this.http.put<boolean>(`${this.base}/reports/schedules/${id}`, body);
  }

  deleteReportSchedule(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.base}/reports/schedules/${id}`);
  }

  getServiceHistory(filters?: {
    vehicleId?: number;
    from?: string;
    to?: string;
    serviceType?: string;
  }): Observable<VehicleServiceHistoryItem[]> {
    let params = new HttpParams();
    if (filters?.vehicleId) params = params.set('vehicleId', filters.vehicleId);
    if (filters?.from) params = params.set('from', filters.from);
    if (filters?.to) params = params.set('to', filters.to);
    if (filters?.serviceType) params = params.set('serviceType', filters.serviceType);
    return this.http.get<VehicleServiceHistoryItem[]>(`${this.base}/history`, { params });
  }
}

function sanitizeCreateWorkOrderPayload(body: CreateWorkOrderPayload): CreateWorkOrderPayload {
  const vehicleId = Number(body.vehicleId) || 0;
  const workshopId = Number(body.workshopId) || 0;
  const technicianId = Number(body.technicianId) || 0;
  const notes = body.notes?.trim();
  const maintenanceType = body.maintenanceType?.trim() || 'Preventive';
  const serviceTypeName = body.serviceTypeName?.trim() || null;

  return {
    vehicleId,
    priority: body.priority?.trim() || 'Medium',
    maintenanceType,
    serviceTypeName,
    startDate: body.startDate || null,
    estimatedCompletionDate: body.estimatedCompletionDate || null,
    laborCost: roundMoney(body.laborCost),
    partsCost: roundMoney(body.partsCost),
    notes: notes || null,
    ...(workshopId > 0 ? { workshopId } : {}),
    ...(technicianId > 0 ? { technicianId } : {}),
    ...(body.requestId && body.requestId > 0 ? { requestId: body.requestId } : {}),
    ...(body.serviceTypeId && body.serviceTypeId > 0 ? { serviceTypeId: body.serviceTypeId } : {})
  };
}

function roundMoney(value: number | undefined): number {
  const amount = Number(value);
  if (!Number.isFinite(amount) || amount < 0) return 0;
  return Math.round(amount * 100) / 100;
}
