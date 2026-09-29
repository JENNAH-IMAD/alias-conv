import { Check, CircleAlert, Clock, LoaderCircle } from 'lucide-react';
import { Badge } from './badge';

const statuses = {
  Completed: { label: 'Terminée', icon: Check, className: 'border-success/20 bg-success/10 text-[var(--success-text)]' },
  Failed: { label: 'Échouée', icon: CircleAlert, className: 'border-destructive/20 bg-destructive/10 text-destructive' },
  Pending: { label: 'En attente', icon: Clock, className: 'border-warning/20 bg-warning/10 text-[var(--warning-text)]' },
  Processing: { label: 'En cours', icon: LoaderCircle, className: 'border-primary/20 bg-primary/10 text-[var(--link)]' },
};

export function StatusBadge({ status }: { status: string }) {
  const info = statuses[status as keyof typeof statuses];
  if (!info) return <Badge variant="secondary">{status}</Badge>;
  const Icon = info.icon;
  return <Badge variant="outline" className={info.className}><Icon aria-hidden="true" className={status === 'Processing' ? 'animate-spin' : ''}/>{info.label}</Badge>;
}
