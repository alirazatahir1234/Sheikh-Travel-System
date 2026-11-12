import { VehicleDocument } from '../../../../core/models/vehicle.model';
import { WIZARD_DOCUMENT_SLOTS } from '../models/vehicle-wizard.model';
import {
  hydrateVehicleDocumentSlots,
  readVehicleDocumentId,
  readVehicleFileUrl,
  slotHasUploadedDocument,
  unwrapUploadVehicleDocumentResult
} from './vehicle-document-slots.util';

describe('vehicle-document-slots.util', () => {
  it('unwraps nested ApiResponse data envelopes', () => {
    const inner = { documentId: 42, fileUrl: 'https://cdn.example/a.jpeg', documentType: 'VehicleImage' };
    expect(unwrapUploadVehicleDocumentResult({ success: true, data: inner })).toEqual(inner);
    expect(unwrapUploadVehicleDocumentResult({ Data: inner })).toEqual(inner);
    expect(unwrapUploadVehicleDocumentResult(inner)).toEqual(inner);
  });

  it('reads camelCase and PascalCase document ids / urls', () => {
    expect(readVehicleDocumentId({ DocumentId: 9 })).toBe(9);
    expect(readVehicleDocumentId({ id: 11 })).toBe(11);
    expect(readVehicleFileUrl({ FileUrl: 'vehicles/1/2/a.png' })).toBe('vehicles/1/2/a.png');
  });

  it('treats documentId alone as uploaded for validation', () => {
    expect(slotHasUploadedDocument({ documentId: 5 })).toBeTrue();
    expect(slotHasUploadedDocument({ fileUrl: 'x.jpeg' })).toBeTrue();
    expect(slotHasUploadedDocument({})).toBeFalse();
  });

  it('hydrates clarify/edit image and document slots from GetDocuments payload', () => {
    const documents: VehicleDocument[] = [
      {
        id: 1,
        vehicleId: 10,
        documentType: 'Registration',
        fileUrl: 'vehicles/1/10/reg.png'
      },
      {
        id: 2,
        vehicleId: 10,
        documentType: 'VehicleImage',
        fileUrl: 'vehicles/1/10/front.jpeg',
        notes: 'Front|primary'
      },
      {
        id: 3,
        vehicleId: 10,
        documentType: 'VehicleImage',
        fileUrl: 'vehicles/1/10/side.jpeg',
        notes: 'Side'
      }
    ];

    const { imageSlots, documentSlots } = hydrateVehicleDocumentSlots(
      documents,
      WIZARD_DOCUMENT_SLOTS.map(s => ({ ...s }))
    );

    expect(imageSlots.find(s => s.angle === 'Front')?.fileUrl).toContain('front.jpeg');
    expect(imageSlots.find(s => s.angle === 'Front')?.isPrimary).toBeTrue();
    expect(imageSlots.find(s => s.angle === 'Side')?.documentId).toBe(3);
    expect(documentSlots.find(s => s.documentType === 'Registration')?.fileUrl).toContain('reg.png');
    expect(documentSlots.find(s => s.documentType === 'Insurance')?.fileUrl).toBeUndefined();
  });

  it('still hydrates when documentType casing differs', () => {
    const documents: VehicleDocument[] = [
      {
        id: 7,
        vehicleId: 10,
        documentType: 'registration',
        fileUrl: 'vehicles/1/10/reg.pdf'
      }
    ];
    const { documentSlots } = hydrateVehicleDocumentSlots(
      documents,
      WIZARD_DOCUMENT_SLOTS.map(s => ({ ...s }))
    );
    expect(documentSlots.find(s => s.documentType === 'Registration')?.documentId).toBe(7);
  });
});
