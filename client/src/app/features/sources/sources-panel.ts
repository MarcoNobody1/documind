import { Component, signal } from '@angular/core';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSeparatorImports } from '@spartan-ng/helm/separator';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';

import { ChatService } from '../../core/chat.service';
import { DocumentsService } from '../../core/documents.service';
import { uploadErrorMessage } from '../../core/upload-error';
import { UploadControl } from '../upload/upload';
import { DocumentCard } from './document-card';

/**
 * Container for the sources sidebar/drawer (SHELL-2, SHELL-3). Injects both `DocumentsService`
 * and `ChatService` — it is the one place that must own both, because reload-after-upload has to
 * live where `documents()` is owned (ADR-M/ADR-P). `UploadControl` stays presentational;
 * `uploadDocument` stays on `ChatService` (relocating it would edit the highest-regression-risk
 * file in the client for no user-visible benefit).
 */
@Component({
  selector: 'app-sources-panel',
  imports: [UploadControl, DocumentCard, ...HlmSeparatorImports, ...HlmSkeletonImports, ...HlmCardImports],
  templateUrl: './sources-panel.html',
  host: { class: 'flex h-full min-h-0 flex-col' }
})
export class SourcesPanel {
  readonly isUploading = signal(false);
  readonly uploadMessage = signal<string | null>(null);
  readonly uploadMessageIsWarning = signal(false);

  constructor(
    readonly documentsService: DocumentsService,
    private readonly chatService: ChatService
  ) {
    void this.documentsService.load();
  }

  async onFileSelected(file: File): Promise<void> {
    this.isUploading.set(true);
    this.uploadMessage.set(null);

    try {
      const result = await this.chatService.uploadDocument(file);

      if (result.warning) {
        this.uploadMessageIsWarning.set(true);
        this.uploadMessage.set(result.warning);
      } else {
        this.uploadMessageIsWarning.set(false);
        this.uploadMessage.set(
          `Uploaded "${file.name}": ${result.pageCount} page(s), ${result.chunkCount} chunk(s).`
        );
      }

      // Refresh so the panel reflects the new document (spec: "Panel refreshes after a successful
      // upload"). Runs even when `result.warning` is set — a warning still means the document was
      // persisted (see `ChatService.uploadDocument`'s contract), just with a caveat.
      await this.documentsService.load();
    } catch (error) {
      this.uploadMessageIsWarning.set(true);
      this.uploadMessage.set(uploadErrorMessage(error, file.name));
    } finally {
      this.isUploading.set(false);
    }
  }
}
