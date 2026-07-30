import { Component } from '@angular/core';

import { Chat } from '../chat/chat';
import { Upload } from '../upload/upload';

/**
 * The authenticated shell: upload + chat, unchanged from before routing existed. Split out of
 * `App` so `App` can hold only the `<router-outlet />` and `authGuard` can guard this route
 * without guarding `/login` and `/register`.
 */
@Component({
  selector: 'app-home',
  imports: [Upload, Chat],
  templateUrl: './home.html'
})
export class Home {}
