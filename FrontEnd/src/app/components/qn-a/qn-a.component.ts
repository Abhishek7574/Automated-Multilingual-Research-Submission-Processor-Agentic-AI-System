import { Component, Input } from '@angular/core';
import { ApiService } from '../../services/api.services';



interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
}


@Component({
  selector: 'app-qn-a',
  templateUrl: './qn-a.component.html',
  styleUrls: ['./qn-a.component.css']
})
export class QnAComponent {
  @Input() documentId!: string;

  question = '';
  sessionId = crypto.randomUUID();
  loading = false;

  messages: ChatMessage[] = [];

  constructor(private api: ApiService) { }

  ask(): void {

    if (!this.question.trim()) return;

    const userQuestion = this.question;

    this.messages.push({
      role: 'user',
      text: userQuestion
    });

    this.question = '';
    this.loading = true;

    this.api.askQuestion(this.documentId, userQuestion, this.sessionId)
      .subscribe({

        next: (res: any) => {

          this.messages.push({
            role: 'assistant',
            text: res.answer ?? "No answer found."
          });

          this.loading = false;

        },

        error: () => {

          this.messages.push({
            role: 'assistant',
            text: "Error retrieving answer."
          });

          this.loading = false;

        }

      });

  }
}
