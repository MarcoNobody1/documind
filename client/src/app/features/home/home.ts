import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, signal, Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmSheetImports } from '@spartan-ng/helm/sheet';
import { BrnSheetContent } from '@spartan-ng/brain/sheet';
import { map } from 'rxjs';

import { Chat } from '../chat/chat';
import { SourcesPanel } from '../sources/sources-panel';

/**
 * `lg` (1024px, ADR-R): the smallest Tailwind breakpoint at which a 280px sidebar and a
 * comfortable (~65ch) answer column are both simultaneously usable.
 */
const DESKTOP_QUERY = '(min-width: 1024px)';

/**
 * The authenticated two-pane shell (SHELL-1): a sources sidebar and the conversation pane, side
 * by side at `lg` and above. Below that breakpoint the sidebar becomes an off-canvas drawer
 * (SHELL-3) built on spartan/ui's `hlm-sheet` (CDK Dialog + Overlay + A11y). The desktop and
 * drawer arms below are mutually exclusive `@if`/`@else` branches — never both present at once —
 * so `SourcesPanel` (and the `DocumentsService.load()` its constructor triggers) is instantiated
 * exactly once at any given time. Declares its host display per ADR-L: Angular's custom-element
 * default is `display: inline`, and this component has no stylesheet to fall back on.
 */
@Component({
  selector: 'app-shell',
  imports: [Chat, SourcesPanel, BrnSheetContent, ...HlmSheetImports, ...HlmButtonImports],
  templateUrl: './home.html',
  host: {
    class: 'flex min-h-0 flex-1',
    '[class.flex-row]': 'isDesktop()',
    '[class.flex-col]': '!isDesktop()'
  }
})
export class AppShell {
  readonly drawerOpen = signal(false);

  readonly isDesktop: Signal<boolean>;

  constructor(breakpointObserver: BreakpointObserver) {
    const initialValue =
      typeof window !== 'undefined' ? window.matchMedia(DESKTOP_QUERY).matches : false;

    this.isDesktop = toSignal(
      breakpointObserver.observe(DESKTOP_QUERY).pipe(map((state) => state.matches)),
      { initialValue }
    );
  }
}
