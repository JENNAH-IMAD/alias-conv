import type { ReactNode } from 'react';
import type { CSSProperties } from 'react';
import type { LucideIcon } from 'lucide-react';
import styles from './page-header.module.css';

export function PageHeader({
  eyebrow,
  title,
  description,
  icon: Icon,
  actions,
  children,
  className = '',
}: {
  eyebrow: string;
  title: string;
  description: string;
  icon: LucideIcon;
  actions?: ReactNode;
  children?: ReactNode;
  className?: string;
}) {
  const titleLetters = Array.from(title);
  return (
    <header className={`${styles.header} alias-page-header ${className}`} data-page-header>
      <Icon className={styles.art} strokeWidth={1.5} aria-hidden="true" />
      <div className={styles.copy}>
        <div className={styles.text}>
          <p className={styles.eyebrow} data-page-eyebrow>
            {eyebrow}
          </p>
          <h1 className={`${styles.title} fern-title-letters`} aria-label={title}>
            {titleLetters.map((letter, index) => (
              <span
                key={`${index}-${letter}`}
                aria-hidden="true"
                style={{ '--fern-letter-index': index } as CSSProperties}
              >
                {letter === ' ' ? '\u00a0' : letter}
              </span>
            ))}
          </h1>
          <p className={styles.description} data-page-description>
            {description}
          </p>
        </div>
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
      {children && <div className={styles.footer}>{children}</div>}
    </header>
  );
}
