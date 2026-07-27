import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { Chat } from './features/chat/chat';
import { Upload } from './features/upload/upload';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Upload, Chat],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {}
