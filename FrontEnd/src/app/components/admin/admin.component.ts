
import { Component, OnInit } from '@angular/core';
import { DocumentSummary } from '../../interfaces/interfaces';
import { ApiService } from '../../services/api.services';
import { AuthService } from '../../services/auth.services';
import { QnAResponse } from '../../services/api.services';
import { Router } from '@angular/router';


@Component({
  selector: 'app-admin',
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {

  documents: DocumentSummary[] = [];
  loading: boolean = false;
  actionMessage: { text: string; type: 'success' | 'error' } | null = null;
  rejectionReasons: Record<string, string> = {};
  processingIds: Set<string> = new Set();

    /* ============================= */
    /* Q&A STATE */
    /* ============================= */

    showQA = false;
    selectedDocumentId: string | null = null;
    question = '';
    asking = false;

    chatHistory: {
      question: string;
      answer: string;
    }[] = [];

  constructor(
    public auth: AuthService,
    private apiService: ApiService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadDocuments();
  }

  get awaitingReview(): DocumentSummary[] {
    return this.documents.filter(d => d.requiresReview && !d.reviewDecision);
  }

  get processed(): DocumentSummary[] {
    return this.documents.filter(d => !d.requiresReview || !!d.reviewDecision);
  }

  loadDocuments(): void {
    this.loading = true;
    this.apiService.getDocuments().subscribe({
      next: (docs) => {
        this.documents = docs;
        this.loading = false;
      },
      error: () => {
        this.showMessage('Failed to load documents. Is the backend running?', 'error');
        this.loading = false;
      }
    });
  }

  getRejectionReason(documentId: string): string {
    return this.rejectionReasons[documentId] || '';
  }

  setRejectionReason(documentId: string, reason: string): void {
    this.rejectionReasons[documentId] = reason;
  }

  approve(doc: DocumentSummary): void {
    this.setProcessing(doc.documentId, true);

    this.apiService.submitReview(doc.documentId, true).subscribe({
      next: () => {
        this.showMessage(`"${doc.fileName}" approved successfully.`, 'success');
        this.loadDocuments();
        this.setProcessing(doc.documentId, false);
      },
      error: () => {
        this.showMessage('Failed to submit approval.', 'error');
        this.setProcessing(doc.documentId, false);
      }
    });
  }

  reject(doc: DocumentSummary): void {
    const reason = this.getRejectionReason(doc.documentId).trim();

    if (!reason) {
      this.showMessage('Please enter a rejection reason before rejecting.', 'error');
      return;
    }

    this.setProcessing(doc.documentId, true);

    this.apiService.submitReview(doc.documentId, false, reason).subscribe({
      next: () => {
        this.showMessage(`"${doc.fileName}" rejected.`, 'success');
        delete this.rejectionReasons[doc.documentId];
        this.loadDocuments();
        this.setProcessing(doc.documentId, false);
      },
      error: () => {
        this.showMessage('Failed to submit rejection.', 'error');
        this.setProcessing(doc.documentId, false);
      }
    });
  }

  isProcessing(documentId: string): boolean {
    return this.processingIds.has(documentId);
  }

  private setProcessing(documentId: string, state: boolean): void {
    state ? this.processingIds.add(documentId) : this.processingIds.delete(documentId);
  }

  private showMessage(text: string, type: 'success' | 'error'): void {
    this.actionMessage = { text, type };
    setTimeout(() => this.actionMessage = null, 4000);
  }


  /* ============================= */
  /* Q&A FUNCTIONS */
  /* ============================= */

  openQA(): void {
    this.showQA = true;
  }

  closeQA(): void {
    this.showQA = false;
  }

  askQuestion(): void {

    if (!this.question || !this.selectedDocumentId) return;

    this.asking = true;

    this.apiService.askQuestion(
      this.selectedDocumentId,
      this.question,
      'admin-session'
    ).subscribe({

      next: (res: QnAResponse) => {

        this.chatHistory.push({
          question: this.question,
          answer: res.answer
        });

        this.question = '';
        this.asking = false;
      },

      error: () => {
        this.asking = false;
      }

    });

  }

  /* ============================= */
  /* AUTH */
  /* ============================= */

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

}
