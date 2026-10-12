export const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
export type Client = {
  id: string;
  name: string;
  legalName: string;
  ice: string;
  if: string;
  rc: string;
  email: string;
  phone: string;
  country: string;
  banks: string[];
  photoVersion: string | null;
  status: string;
};
export type Bank = {
  id: string;
  name: string;
  code: string;
  description: string;
  clients: number;
  status: string;
  accounts: number;
  logoVersion: string | null;
};
export type Account = {
  id: string;
  clientId: string;
  bankId: string;
  bank: string;
  accountNumber: string;
  accountName: string;
  currency: string;
  journal: string;
  accountCode: string;
};
export type Document = {
  id: string;
  clientId: string;
  bankAccountId: string;
  client: string;
  bank: string;
  accountName: string;
  accountNumber: string;
  fileName: string;
  createdAt: string;
};
export type Dashboard = {
  clients: number;
  banks: number;
  accounts: number;
  statements: number;
  exports: number;
  unassigned: number;
  pendingReview: number;
  statuses: { status: string; count: number }[];
  activity: { date: string; imports: number; exports: number }[];
  recent: {
    id: string;
    sourceFileName: string | null;
    bankName: string;
    statementNumber: string | null;
    clientId: string | null;
    clientName: string | null;
    accountName: string | null;
    status: string;
    transactionCount: number;
    createdAt: string;
  }[];
  updatedAt: string;
};
export type ExportField = {
  fieldName: string;
  sourceField: string;
  position: number;
  format: string | null;
  required: boolean;
};
export type ExportTemplate = {
  id: string;
  name: string;
  type: 'CUSTOMCSV' | 'SAGE100' | 'SAGEX3';
  description: string;
  fileExtension: string;
  delimiter: string;
  encoding: string;
  numberCulture: string;
  includeHeader: boolean;
  isActive: boolean;
  fields: ExportField[];
};

export type ExportDiagnostic = {
  code: string;
  message: string;
  transactionSequence: number | null;
  fieldName: string | null;
  sourceField: string | null;
};
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public diagnostics: ExportDiagnostic[] = [],
    public warnings: ExportDiagnostic[] = [],
  ) {
    super(message);
  }
}
export async function apiError(response: Response): Promise<ApiError> {
  const problem = await response.json().catch(() => null);
  const diagnostics: ExportDiagnostic[] = Array.isArray(problem?.errors)
    ? problem.errors.filter((e: unknown) => typeof e === 'object' && e !== null && 'code' in e)
    : [];
  const validation =
    !Array.isArray(problem?.errors) && problem?.errors
      ? Object.values(problem.errors).flat().join(' ')
      : '';
  return new ApiError(
    validation || problem?.title || diagnostics[0]?.message || `Erreur HTTP ${response.status}`,
    response.status,
    diagnostics,
    problem?.warnings || [],
  );
}

export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('X-Alias-Request', '1');
  if (options.body && !(options.body instanceof FormData))
    headers.set('Content-Type', 'application/json');
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      ...options,
      headers,
      credentials: 'include',
      cache: 'no-store',
    });
  } catch {
    throw new Error('API inaccessible. Vérifiez que le backend est démarré.');
  }
  if (!response.ok) {
    if (response.status === 401 && !path.startsWith('/auth/') && typeof window !== 'undefined')
      window.dispatchEvent(new Event('alias-session-expired'));
    throw await apiError(response);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
export async function all<T>(path: string, signal?: AbortSignal): Promise<T[]> {
  const result: T[] = [];
  let page = 1;
  while (true) {
    const data = await request<Page<T>>(
      `${path}${path.includes('?') ? '&' : '?'}page=${page}&pageSize=100`,
      { signal },
    );
    result.push(...data.items);
    if (result.length >= data.total || !data.items.length) return result;
    page++;
  }
}

export async function documentBlob(path: string): Promise<Blob> {
  const response = await fetch(API_URL + path, {
    cache: 'no-store',
    credentials: 'include',
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.title || 'Téléchargement impossible.');
  }
  return response.blob();
}
export function saveBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name.replace(/[\\/\r\n]/g, '_');
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 10000);
}
