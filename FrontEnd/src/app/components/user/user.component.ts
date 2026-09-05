import { Component, OnInit } from '@angular/core';
import { ApiService, QnAResponse } from '../../services/api.services';
import { AuthService } from '../../services/auth.services';
import {
  DocumentSummary,
  PipelineStepMeta,
  PipelineResult,
  StepResult,
  DocumentMetadata
} from '../../interfaces/interfaces';

export type StepState = 'pending' | 'active' | 'done' | 'error';
export type UploadStatus = 'idle' | 'processing' | 'done' | 'error';

export interface PipelineStep {
  id: number;
  icon: string;
  title: string;
  description: string;
  state: StepState;
  detail?: string;
}

@Component({
  selector: 'app-user',
  templateUrl: './user.component.html',
  styleUrls: ['./user.component.css']
})
export class UserComponent implements OnInit {
  selectedFile: File | null = null;
  uploadStatus: UploadStatus = 'idle';
  isDragOver = false;
  currentStepIndex = -1;
  apiError: string | null = null;
  totalElapsedMs: number | null = null;

  // New property to hold the result for the UI card
  lastResult: PipelineResult | null = null;
  extractedMeta: DocumentMetadata | null = null;

  steps: PipelineStep[] = [];
  mySubmissions: DocumentSummary[] = [];
  submissionsLoading = false;
  activeTab: 'upload' | 'submissions' | 'qna' = 'upload';
  selectedDocumentId: string | null = null;
  question = '';
  asking = false;
  chatHistory: { question: string; answer: string; }[] = [];

  constructor(public auth: AuthService, private apiService: ApiService) { }

  get currentUsername(): string | null { return this.auth.getCurrentUser(); }

  ngOnInit(): void {
    this.apiService.getPipelineSteps().subscribe({
      next: (stepsFromApi: PipelineStepMeta[]) => {
        this.steps = stepsFromApi.map(s => ({
          id: s.id,
          icon: s.icon,
          title: s.name,
          description: s.description,
          state: 'pending'
        }));
      }
    });
    this.loadMySubmissions();
  }

  get completedSteps(): number { return this.steps.filter(s => s.state === 'done').length; }
  get progressPercent(): number {
    if (!this.steps.length) return 0;
    return Math.round((this.completedSteps / this.steps.length) * 100);
  }

  loadMySubmissions(): void {
    this.submissionsLoading = true;
    this.apiService.getDocuments().subscribe({
      next: (docs) => { this.mySubmissions = docs; this.submissionsLoading = false; },
      error: () => this.submissionsLoading = false
    });
  }

  switchTab(tab: 'upload' | 'submissions' | 'qna'): void {
    this.activeTab = tab;
    if (tab === 'submissions' || tab === 'qna') this.loadMySubmissions();
  }

  reviewStatusLabel(doc: DocumentSummary): string {
    if (!doc.requiresReview) return 'Auto-Approved';
    if (!doc.reviewDecision) return 'Pending Review';
    return doc.reviewDecision.approved ? 'Approved' : 'Rejected';
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.setFile(input.files[0]);
  }

  onDragOver(event: DragEvent): void { event.preventDefault(); this.isDragOver = true; }
  onDragLeave(): void { this.isDragOver = false; }
  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver = false;
    const file = event.dataTransfer?.files[0];
    if (file) this.setFile(file);
  }

  private setFile(file: File): void {
    const allowedTypes = ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'application/msword'];
    if (!allowedTypes.includes(file.type)) return;
    this.selectedFile = file;
    this.uploadStatus = 'idle';
    this.lastResult = null; // Reset result on new file
    this.resetSteps();
  }

  private resetSteps(): void {
    this.currentStepIndex = -1;
    this.steps = this.steps.map(s => ({ ...s, state: 'pending', detail: undefined }));
  }

  startProcessing(): void {
    if (!this.selectedFile) return;
    this.uploadStatus = 'processing';
    this.apiError = null;
    this.lastResult = null;
    this.resetSteps();
    this.setStepState(0, 'active');

    this.apiService.uploadDocument(this.selectedFile).subscribe({
      next: (result) => {
        this.lastResult = result;
        // Attempt to extract metadata from the 'extraction' step data
        if (result.extraction?.data) {
          this.extractedMeta = result.extraction.data as DocumentMetadata;
        }
        this.animateResults(result);
      },
      error: (err) => {
        this.apiError = err?.error?.error ?? err?.message ?? 'Upload failed.';
        this.uploadStatus = 'error';
        this.steps = this.steps.map(s => s.state === 'active' || s.state === 'pending' ? { ...s, state: 'error' } : s);
      }
    });
  }

  private animateResults(result: PipelineResult): void {
    this.totalElapsedMs = result.totalElapsedMs;
    const stepResults: StepResult[] = [
      result.ingestion, result.preProcess, result.translation, result.extraction,
      result.validation, result.contentSafety, result.plagiarism, result.ragIndex,
      result.summarization, result.qnA, result.humanFeedback
    ];

    const STEP_DELAY = 350;
    stepResults.forEach((stepResult, i) => {
      setTimeout(() => this.setStepState(i, 'active'), i * STEP_DELAY * 2);
      setTimeout(() => {
        this.setStepState(i, stepResult.success ? 'done' : 'error', stepResult.error ?? undefined);
      }, i * STEP_DELAY * 2 + STEP_DELAY);
    });

    setTimeout(() => {
      this.uploadStatus = 'done';
      this.loadMySubmissions();
    }, stepResults.length * STEP_DELAY * 2 + STEP_DELAY);
  }

  private setStepState(index: number, state: StepState, detail?: string): void {
    this.currentStepIndex = index;
    this.steps = this.steps.map((s, i) => i === index ? { ...s, state, detail } : s);
  }

  reset(): void {
    this.selectedFile = null;
    this.uploadStatus = 'idle';
    this.apiError = null;
    this.lastResult = null;
    this.extractedMeta = null;
    this.resetSteps();
  }

  formatFileSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  askQuestion(): void {
    if (!this.question || !this.selectedDocumentId) return;
    this.asking = true;
    this.apiService.askQuestion(this.selectedDocumentId, this.question, 'user-session').subscribe({
      next: (res: QnAResponse) => {
        this.chatHistory.push({ question: this.question, answer: res.answer });
        this.question = '';
        this.asking = false;
      },
      error: () => this.asking = false
    });
  }
}
