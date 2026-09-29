import type { User } from '@/services/api';

const legacyPermissions = ['dashboard.read', 'clients.read', 'clients.write', 'banks.read', 'accounts.read', 'accounts.write', 'profiles.read', 'templates.read', 'documents.read', 'documents.write', 'documents.export'];
export function can(user: User | null | undefined, permission: string) {
  return !!user && (user.role === 'Admin' || (user.permissions ?? legacyPermissions).includes(permission));
}
export function pagePermission(path: string): string | undefined {
  if (path.startsWith('/conversions') || path.startsWith('/client-workspace')) return 'documents.read';
  return ({ '/dashboard': 'dashboard.read', '/clients': 'clients.read', '/banks': 'banks.read', '/bank-accounts': 'accounts.read', '/templates': 'templates.read', '/extraction-profiles': 'profiles.read', '/users': 'admin.only' } as Record<string, string>)[path];
}
