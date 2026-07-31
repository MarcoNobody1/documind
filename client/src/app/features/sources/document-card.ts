import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { HlmCardImports } from '@spartan-ng/helm/card';

import { DocumentListItem } from '../../core/models';

/** One row in the sources panel's document list. Purely presentational (SHELL-2). */
@Component({
  selector: 'app-document-card',
  imports: [DatePipe, ...HlmCardImports],
  templateUrl: './document-card.html',
  host: { class: 'block' }
})
export class DocumentCard {
  readonly document = input.required<DocumentListItem>();
}
