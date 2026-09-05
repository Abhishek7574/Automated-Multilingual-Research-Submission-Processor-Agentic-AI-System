import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PipelineStepMeta {
  id: number;
  name: string;
  icon: string;
  description: string;
}

export interface FlaggedItemSummary {
  field: string;
  agentResult: string;
  confidence: number;
  humanCorrection: string | null;
}

export interface ReviewDecision {
  documentId: string;
  approved: boolean;
  rejectionReason: string | null;
  reviewedBy: string;
  decidedAt: string;
}

export interface DocumentSummary {
  documentId: string;
  fileName: string;
  overallSuccess: boolean;
  totalElapsedMs: number;
  requiresReview: boolean;
  isResolved: boolean;
  overallConfidence: number;
  flaggedItems: FlaggedItemSummary[];
  processedAt: string;
  reviewDecision?: ReviewDecision;
}

export interface HealthResponse {
  version: string;
  status: string;
  timestamp: string;
}

export interface StepResult {
  success: boolean;
  data: unknown;
  error: string | null;
  elapsedMs: number;
}

export interface PipelineResult {
  documentId: string;
  fileName: string;
  overallSuccess: boolean;

  ingestion: StepResult;
  preProcess: StepResult;
  translation: StepResult;
  extraction: StepResult;
  validation: StepResult;
  contentSafety: StepResult;
  plagiarism: StepResult;
  ragIndex: StepResult;
  summarization: StepResult;
  qnA: StepResult;
  humanFeedback: StepResult;

  totalElapsedMs: number;
}

/* ============================= */
/* Q&A RESPONSE */
/* ============================= */

export interface QnAResponse {
  question: string;
  answer: string;
  sourceChunks: string[];
  confidence: number;
  sessionId: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {

  private baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(`https://localhost:7137/api/health`);
  }

  getPipelineSteps(): Observable<PipelineStepMeta[]> {
    return this.http.get<PipelineStepMeta[]>(`https://localhost:7137/api/documents/pipeline-steps`);
  }

  getDocuments(): Observable<DocumentSummary[]> {
    return this.http.get<DocumentSummary[]>(`https://localhost:7137/api/documents`);
  }

  submitReview(
    documentId: string,
    approved: boolean,
    rejectionReason?: string,
  ): Observable<ReviewDecision> {

    return this.http.post<ReviewDecision>(
      `https://localhost:7137/api/documents/${documentId}/review`,
      {
        approved,
        rejectionReason: rejectionReason ?? null,
      }
    );
  }

  uploadDocument(file: File): Observable<PipelineResult> {

    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<PipelineResult>(
      `https://localhost:7137/api/documents/process`,
      formData
    );
  }

  /* ============================= */
  /* ASK QUESTION */
  /* ============================= */

  askQuestion(
    documentId: string,
    question: string,
    sessionId?: string
  ): Observable<QnAResponse> {

    return this.http.post<QnAResponse>(
      `https://localhost:7137/api/documents/${documentId}/ask`,
      {
        documentId,
        question,
        sessionId
      }
    );
  }

  submitHitlCorrection(
    documentId: string,
    field: string,
    correction: string
  ): Observable<unknown> {

    return this.http.post(
      `https://localhost:7137/api/documents/${documentId}/correct`,
      {
        field,
        correction,
      }
    );
  }

}
