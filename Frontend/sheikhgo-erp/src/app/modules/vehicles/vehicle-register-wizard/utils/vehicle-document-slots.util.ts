import { VehicleDocument, UploadVehicleDocumentResult } from '../../../../core/models/vehicle.model';
import {
  DocumentSlotState,
  VEHICLE_IMAGE_ANGLES,
  VehicleImageSlotState,
  parseVehicleImageAngle,
  isPrimaryVehicleImage
} from '../models/vehicle-wizard.model';

/** Normalize API / envelope shapes for document upload responses. */
export function unwrapUploadVehicleDocumentResult(raw: unknown): UploadVehicleDocumentResult {
  if (!raw || typeof raw !== 'object') {
    return raw as UploadVehicleDocumentResult;
  }
  const rec = raw as Record<string, unknown>;
  if (rec['data'] && typeof rec['data'] === 'object') {
    return rec['data'] as UploadVehicleDocumentResult;
  }
  if (rec['Data'] && typeof rec['Data'] === 'object') {
    return rec['Data'] as UploadVehicleDocumentResult;
  }
  return raw as UploadVehicleDocumentResult;
}

export function readVehicleDocumentId(
  value: VehicleImageSlotState | DocumentSlotState | VehicleDocument | UploadVehicleDocumentResult | Record<string, unknown> | null | undefined
): number | undefined {
  if (!value || typeof value !== 'object') return undefined;
  const rec = value as Record<string, unknown>;
  const raw = rec['documentId'] ?? rec['DocumentId'] ?? rec['id'] ?? rec['Id'];
  const num = Number(raw);
  return Number.isFinite(num) && num > 0 ? Math.trunc(num) : undefined;
}

export function readVehicleFileUrl(value: unknown): string | undefined {
  if (!value || typeof value !== 'object') return undefined;
  const rec = value as Record<string, unknown>;
  const raw = rec['fileUrl'] ?? rec['FileUrl'];
  return typeof raw === 'string' && raw.trim() ? raw.trim() : undefined;
}

/** Slot counts as uploaded when a remote URL or persisted document id is present. */
export function slotHasUploadedDocument(
  slot: Pick<DocumentSlotState, 'fileUrl' | 'documentId'> | Pick<VehicleImageSlotState, 'fileUrl' | 'documentId'>
): boolean {
  return !!slot.fileUrl?.trim() || (slot.documentId != null && slot.documentId > 0);
}

function sameDocumentType(a: string | null | undefined, b: string | null | undefined): boolean {
  return (a ?? '').trim().toLowerCase() === (b ?? '').trim().toLowerCase();
}

/**
 * Map API vehicle documents into wizard image + document slot state for edit/clarify hydrate.
 */
export function hydrateVehicleDocumentSlots(
  documents: VehicleDocument[],
  currentDocumentSlots: DocumentSlotState[]
): { imageSlots: VehicleImageSlotState[]; documentSlots: DocumentSlotState[] } {
  const imageDocs = documents.filter(d => sameDocumentType(d.documentType, 'VehicleImage'));
  const slots: VehicleImageSlotState[] = VEHICLE_IMAGE_ANGLES.map(({ angle, label }) => ({ angle, label }));
  const assignedDocIds = new Set<number>();

  for (const slot of slots) {
    const doc = imageDocs.find(d => {
      const id = readVehicleDocumentId(d);
      return id != null
        && !assignedDocIds.has(id)
        && parseVehicleImageAngle(d.notes) === slot.angle;
    });
    if (!doc) continue;
    const id = readVehicleDocumentId(doc)!;
    assignedDocIds.add(id);
    slot.fileUrl = doc.fileUrl?.trim() || undefined;
    slot.documentId = id;
    slot.isPrimary = isPrimaryVehicleImage(doc.notes);
  }

  const unassigned = imageDocs.filter(d => {
    const id = readVehicleDocumentId(d);
    return id != null && !assignedDocIds.has(id);
  });
  const openSlots = slots.filter(s => !slotHasUploadedDocument(s));
  for (let i = 0; i < unassigned.length && i < openSlots.length; i++) {
    const doc = unassigned[i];
    const slot = openSlots[i];
    const id = readVehicleDocumentId(doc)!;
    assignedDocIds.add(id);
    slot.fileUrl = doc.fileUrl?.trim() || undefined;
    slot.documentId = id;
    slot.isPrimary = isPrimaryVehicleImage(doc.notes);
  }

  if (!slots.some(s => s.isPrimary)) {
    const first = slots.find(s => slotHasUploadedDocument(s));
    if (first) first.isPrimary = true;
  }

  const documentSlots = currentDocumentSlots.map(slot => {
    const doc = documents.find(d =>
      sameDocumentType(d.documentType, slot.documentType)
      && (!!d.fileUrl?.trim() || readVehicleDocumentId(d) != null)
    );
    if (!doc) {
      return {
        ...slot,
        fileUrl: undefined,
        documentId: undefined,
        file: undefined,
        uploading: false,
        error: slot.error
      };
    }
    return {
      ...slot,
      fileUrl: doc.fileUrl?.trim() || undefined,
      documentId: readVehicleDocumentId(doc),
      file: undefined,
      uploading: false,
      error: undefined
    };
  });

  return { imageSlots: slots, documentSlots };
}
