export const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5081/api';
export type User = { id: string; name: string; email: string; role: string; permissions?: string[] };
export type Session = { token: string; expiresAt: string; user: User };
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
export type Client = { id: string; name: string; legalName: string; ice: string; if: string; rc: string; email: string; phone: string; country: string; banks: string[]; photoVersion: string | null; status: string };
export type Bank = { id: string; name: string; code: string; description: string; clients: number; status: string; accounts: number; profiles: number; activeProfiles: number; logoVersion: string | null };
export type Account = { id: string; clientId: string; bankId: string; bank: string; accountNumber: string; accountName: string; currency: string; journal: string; accountCode: string };
export type Statement = { id: string; bankId: string | null; bank: string; name: string; description: string; parserKey: string; delimiter: string; dateFormat: string; numberCulture: string; skipLines: number; dateColumn: number; descriptionColumn: number; debitColumn: number; creditColumn: number; balanceColumn: number | null; referenceColumn: number | null; isActive: boolean };
export type ExportField = { fieldName: string; sourceField: string; position: number; format: string | null; required: boolean };
export type ExportTemplate = { id: string; name: string; type: string; description: string; fileExtension: string; delimiter: string; encoding: string; numberCulture: string; includeHeader: boolean; isActive: boolean; fields: ExportField[] };
export type History = { id: string; clientId: string; bankAccountId: string; client: string; bank: string; inputFileName: string; outputFileName: string | null; status: string; errorMessage: string | null; count: number; createdAt: string; completedAt: string | null; accountName?: string | null; accountNumber?: string | null; processedByName?: string | null; processedAt?: string | null; modifiedByName?: string | null; modifiedAt?: string | null };
export type Transaction = { id: string; date: string; description: string; reference: string; debit: number; credit: number; amount: number; balance: number | null };
export type Conversion = { history: History; transactions: Transaction[] };
export type Dashboard = { clients: number; banks: number; conversions: number; completed: number; failed: number; recent: History[] };

export function getSession(): Session | null {
  if (typeof window === 'undefined') return null;
  try {
    const session = JSON.parse(sessionStorage.getItem('bank-converter-session') || 'null') as Session | null;
    return session?.token && Date.parse(session.expiresAt) > Date.now() ? session : null;
  } catch { return null; }
}
export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  const session = getSession();
  if (session) headers.set('Authorization', `Bearer ${session.token}`);
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  let response: Response;
  try { response = await fetch(`${API_URL}${path}`, { ...options, headers, cache: 'no-store' }); }
  catch { throw new Error('API inaccessible. Vérifiez que le backend est démarré.'); }
  if (!response.ok) {
    if (response.status === 401 && !path.startsWith('/auth/')) window.dispatchEvent(new Event('session-expired'));
    const problem = await response.json().catch(() => null);
    const errors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new Error(errors || problem?.title || (response.status === 403 ? 'Action réservée aux administrateurs.' : `Erreur HTTP ${response.status}`));
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
export async function all<T>(path: string): Promise<T[]> {
  const result: T[] = [];
  let page = 1;
  while (true) {
    const data = await request<Page<T>>(`${path}?page=${page}&pageSize=100`);
    result.push(...data.items);
    if (result.length >= data.total || !data.items.length) return result;
    page++;
  }
}
export async function download(history: History, excel = false) {
  const response = await fetch(`${API_URL}/conversions/${history.id}/download${excel?'?excel=true':''}`, { headers: { Authorization: `Bearer ${getSession()?.token || ''}` } });
  if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.title || 'Téléchargement impossible.'); }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a'); link.href = url; link.download = excel ? (history.outputFileName || 'conversion.csv').replace(/\.[^.]+$/, '.xlsx') : history.outputFileName || 'conversion.csv'; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function exportBlob(path: string): Promise<Blob> {
  const response = await fetch(API_URL + path, { headers: { Authorization: 'Bearer ' + (getSession()?.token || '') }, cache: 'no-store' });
  if (!response.ok) {
    if (response.status === 401) window.dispatchEvent(new Event('session-expired'));
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.title || 'Téléchargement impossible.');
  }
  return response.blob();
}
