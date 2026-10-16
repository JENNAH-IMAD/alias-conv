'use client';
import { Button } from './button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
export function Pagination({
  page,
  total,
  onChange,
  label = 'Pagination',
  itemLabel,
  loading = false,
}: {
  page: number;
  total: number;
  onChange: (n: number) => void;
  label?: string;
  itemLabel?: string;
  loading?: boolean;
}) {
  return (
    <nav aria-label={label} className="pagination">
      <span>
        {total} {itemLabel || `élément${total > 1 ? 's' : ''}`} · Page {page} sur{' '}
        {Math.max(1, Math.ceil(total / 20))}
      </span>
      <div className="flex gap-2">
        <Button
          variant="outline"
          disabled={loading || page === 1}
          onClick={() => onChange(page - 1)}
        >
          <ChevronLeft size={14} /> Précédent
        </Button>
        <Button
          variant="outline"
          disabled={loading || page * 20 >= total}
          onClick={() => onChange(page + 1)}
        >
          Suivant <ChevronRight size={14} />
        </Button>
      </div>
    </nav>
  );
}
