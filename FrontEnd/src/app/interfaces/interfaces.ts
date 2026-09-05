// Add this to your existing interfaces.ts
export interface DocumentMetadata {
  title: string;
  authors: string;
  pageCount: number;
  language: string;
}

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
  data: any; // Changed to any to access metadata properties
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
