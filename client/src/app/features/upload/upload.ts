import { Component, signal } from '@angular/core';

import { ChatService } from '../../core/chat.service';
import { uploadErrorMessage } from '../../core/upload-error';

/**
 * PDF upload UI. Deliberately minimal/throwaway (see portfolio/documind-ui-future) — a dedicated
 * design slice follows.
 */
@Component({
  selector: 'app-upload',
  templateUrl: './upload.html'
})
export class Upload {
  readonly isUploading = signal(false);
  readonly lastMessage = signal<string | null>(null);
  readonly lastMessageIsWarning = signal(false);

  constructor(private readonly chatService: ChatService) {}

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.isUploading.set(true);
    this.lastMessage.set(null);

    try {
      const result = await this.chatService.uploadDocument(file);

      if (result.warning) {
        this.lastMessageIsWarning.set(true);
        this.lastMessage.set(result.warning);
      } else {
        this.lastMessageIsWarning.set(false);
        this.lastMessage.set(
          `Uploaded "${file.name}": ${result.pageCount} page(s), ${result.chunkCount} chunk(s).`
        );
      }
    } catch (error) {
      this.lastMessageIsWarning.set(true);
      this.lastMessage.set(uploadErrorMessage(error, file.name));
    } finally {
      this.isUploading.set(false);
      input.value = '';
    }
  }
}
