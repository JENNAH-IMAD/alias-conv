import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

export function PageHeader({
  eyebrow,
  title,
  description,
  icon: Icon,
  actions,
}: {
  eyebrow: string;
  title: string;
  description: string;
  icon: LucideIcon;
  actions?: ReactNode;
}) {
  return (
    <section className="ui-hero page-header">
      <div className="page-header-copy">
        <span className="page-header-icon" aria-hidden="true">
          <Icon size={24} strokeWidth={1.7} />
        </span>
        <div>
          <p className="ui-eyebrow">{eyebrow}</p>
          <h1 className="ui-title">{title}</h1>
          <p className="ui-description">{description}</p>
        </div>
      </div>
      {actions && <div className="page-header-actions">{actions}</div>}
    </section>
  );
}
