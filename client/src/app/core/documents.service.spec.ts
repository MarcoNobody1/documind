import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { DocumentsService } from './documents.service';
import { environment } from '../../environments/environment';

describe('DocumentsService', () => {
  let httpMock: HttpTestingController;
  let service: DocumentsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(DocumentsService);
  });

  afterEach(() => httpMock.verify());

  it('populates documents() on a successful load and clears loading/failure state', async () => {
    const load = service.load();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/documents`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { id: 'd1', fileName: 'report.pdf', pageCount: 12, uploadedAtUtc: '2026-01-01T00:00:00Z' }
    ]);

    await load;

    expect(service.documents()).toEqual([
      { id: 'd1', fileName: 'report.pdf', pageCount: 12, uploadedAtUtc: '2026-01-01T00:00:00Z' }
    ]);
    expect(service.isLoading()).toBe(false);
    expect(service.loadFailed()).toBe(false);
  });

  it('sets loadFailed() and leaves documents() empty when the request fails', async () => {
    const load = service.load();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/documents`);
    req.flush(null, { status: 500, statusText: 'Internal Server Error' });

    await load;

    expect(service.documents()).toEqual([]);
    expect(service.loadFailed()).toBe(true);
    expect(service.isLoading()).toBe(false);
  });

  it('sets isLoading() while the request is in flight', () => {
    void service.load();

    expect(service.isLoading()).toBe(true);
    httpMock.expectOne(`${environment.apiBaseUrl}/api/documents`).flush([]);
  });
});
