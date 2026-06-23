export type OcrMode = 'HYBRID' | 'PADDLE_ONLY' | 'AZURE_ONLY';
export type BackendOcrMode = 'Hybrid' | 'PaddleOnly' | 'AzureOnly';

export interface OcrSettings {
  mode: OcrMode;
  confidenceThreshold: number;
  enableFallback: boolean;
  saveRawOcr: boolean;
  azureEnabled: boolean;
  paddleEnabled: boolean;
}

export interface OcrExtractRequest {
  mode: BackendOcrMode;
  confidenceThreshold: number;
  enableFallback: boolean;
  /** When false, API omits raw OCR text (smaller payloads; less client-side re-parsing). */
  includeRawText?: boolean;
}

export interface OcrExtractResult {
  /** User-facing engine label (PaddleOCR, Azure AI, Hybrid, …). */
  ocrEngine?: string;
  /** Legacy; prefer `ocrEngine`. */
  provider?: string;
  confidence?: number;
  fallbackUsed?: boolean;
  /** True when server confidence is below the configured threshold (1–99). */
  lowConfidence?: boolean;
  confidenceThreshold?: number;
  addressTranslated?: boolean;
  /** Hybrid path: Azure merged after weak Paddle result. */
  azureQualityMergeUsed?: boolean;
  primaryOcrEngine?: string | null;
  secondaryOcrEngine?: string | null;
  fullName?: string | null;
  fatherName?: string | null;
  identityNumber?: string | null;
  cnic?: string | null;
  address?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  nationality?: string | null;
  rawText?: string | null;
}
