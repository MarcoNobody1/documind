import { HttpErrorResponse } from '@angular/common/http';

import { uploadErrorMessage } from './upload-error';

describe('uploadErrorMessage', () => {
  it('surfaces the API-provided reason for a rejected upload', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { error: 'The file is not a valid PDF.' }
    });

    expect(uploadErrorMessage(error, 'handbook.pdf')).toBe('The file is not a valid PDF.');
  });

  it('falls back to the generic "Is the API running?" message when no response was received', () => {
    const error = new HttpErrorResponse({ status: 0, error: null });

    expect(uploadErrorMessage(error, 'handbook.pdf')).toBe(
      'Failed to upload "handbook.pdf". Is the API running?'
    );
  });

  it('falls back to an HTTP-status message when the response body has no usable reason', () => {
    const error = new HttpErrorResponse({ status: 500, error: 'Internal Server Error' });

    expect(uploadErrorMessage(error, 'handbook.pdf')).toBe(
      'Upload of "handbook.pdf" was rejected (HTTP 500).'
    );
  });

  it('falls back to the generic message for a non-HttpErrorResponse error', () => {
    expect(uploadErrorMessage(new Error('boom'), 'handbook.pdf')).toBe(
      'Failed to upload "handbook.pdf". Is the API running?'
    );
  });
});
