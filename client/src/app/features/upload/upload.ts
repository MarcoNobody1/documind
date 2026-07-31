import { Component, input, output } from '@angular/core';

/**
 * Presentational PDF upload control, living inside the sources panel (#1988 §3). Emits the
 * selected file and lets its container (`SourcesPanel`) own the upload call and the
 * upload-in-progress/result state, so the container can refresh the document list on success
 * (ADR-P). No service injection here — that is the leaf rule the whole redesign holds to.
 */
@Component({
  selector: 'app-upload-control',
  templateUrl: './upload.html',
  host: { class: 'flex flex-col gap-2' }
})
export class UploadControl {
  readonly isUploading = input.required<boolean>();
  readonly message = input<string | null>(null);
  readonly messageIsWarning = input(false);

  readonly fileSelected = output<File>();

  onFileSelected(event: Event): void {
    const fileInput = event.target as HTMLInputElement;
    const file = fileInput.files?.[0];
    fileInput.value = '';

    if (!file) {
      return;
    }

    this.fileSelected.emit(file);
  }
}
