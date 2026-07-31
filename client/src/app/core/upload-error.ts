import { HttpErrorResponse } from '@angular/common/http';

/**
 * Derives a user-facing message for a failed document upload.
 *
 * The API rejects invalid uploads (non-PDF, empty file, oversized file — see
 * `DocumentsEndpoints.cs`) with a 4xx response whose body is `{ "error": "<reason>" }`, not
 * ProblemDetails. When that reason is available, it is shown verbatim instead of the generic
 * "Is the API running?" message, which is reserved for `status === 0` — the case where no
 * response was received at all (network failure), per the spec's Upload Rejection Reason
 * requirement.
 */
export function uploadErrorMessage(error: unknown, fileName: string): string {
  if (error instanceof HttpErrorResponse && error.status !== 0) {
    const reason = reasonFrom(error.error);
    return reason ?? `Upload of "${fileName}" was rejected (HTTP ${error.status}).`;
  }

  return `Failed to upload "${fileName}". Is the API running?`;
}

function reasonFrom(body: unknown): string | null {
  if (body != null && typeof body === 'object' && 'error' in body) {
    const reason = (body as { error: unknown }).error;
    if (typeof reason === 'string' && reason.length > 0) {
      return reason;
    }
  }
  return null;
}
