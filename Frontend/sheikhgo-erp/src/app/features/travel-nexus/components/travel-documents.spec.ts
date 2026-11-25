import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { APPLICANT_CONDITION_LABEL, DOCUMENT_STATUS_VARIANT, TRAVEL_APPLICANT_CONDITIONS, TravelDocumentStatus } from '../models/travel.models';
import { TravelDocumentService } from '../services/travel-document.service';
import { precheckDocumentFile } from './document-upload.component';
import { DocumentViewerComponent } from './document-viewer.component';

const file = (name: string, size: number) => new File([new Uint8Array(size)], name);

describe('TravelNexus documents (ERP)', () => {
  it('pre-checks extension and size before uploading', () => {
    expect(precheckDocumentFile(file('passport.pdf', 10))).toBeNull();
    expect(precheckDocumentFile(file('photo.JPG', 10))).toBeNull();
    expect(precheckDocumentFile(file('virus.exe', 10))).toContain('Only');
    expect(precheckDocumentFile(file('empty.png', 0))).toContain('empty');
    expect(precheckDocumentFile(file('big.pdf', 11 * 1024 * 1024))).toContain('10 MB');
  });

  it('labels every applicant condition and badges every document status', () => {
    for (const c of TRAVEL_APPLICANT_CONDITIONS) expect(APPLICANT_CONDITION_LABEL[c]).withContext(c).toBeTruthy();
    const statuses: TravelDocumentStatus[] = ['Missing', 'Uploaded', 'Verified', 'Rejected', 'Waived'];
    for (const s of statuses) expect(DOCUMENT_STATUS_VARIANT[s]).withContext(s).toBeTruthy();
  });

  describe('service', () => {
    let http: HttpTestingController;
    let api: TravelDocumentService;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      http = TestBed.inject(HttpTestingController);
      api = TestBed.inject(TravelDocumentService);
    });
    afterEach(() => http.verify());

    it('uploads as multipart with progress and downloads files as blobs (never by URL)', () => {
      api.upload(7, file('passport.pdf', 5)).subscribe();
      const up = http.expectOne(`${environment.apiUrl}/travel/documents/requirements/7/files`);
      expect(up.request.body instanceof FormData).toBeTrue();
      expect(up.request.reportProgress).toBeTrue();
      up.flush(1);

      api.downloadFile(9).subscribe();
      const down = http.expectOne(`${environment.apiUrl}/travel/documents/files/9`);
      expect(down.request.responseType).toBe('blob');
      down.flush(new Blob(['x']));
    });

    it('sends review reasons', () => {
      api.waive(3, 'Embassy exempts minors').subscribe();
      expect(http.expectOne(`${environment.apiUrl}/travel/documents/requirements/3/waive`).request.body).toEqual({ reason: 'Embassy exempts minors' });
    });
  });

  it('viewer revokes the object URL when released', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const viewer = TestBed.runInInjectionContext(() => new DocumentViewerComponent());
    const revoke = spyOn(URL, 'revokeObjectURL');
    (viewer as unknown as { url: { set(v: string): void } }).url.set('blob:http://localhost/abc');
    viewer.release();
    expect(revoke).toHaveBeenCalledWith('blob:http://localhost/abc');
  });
});
