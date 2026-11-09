import { Check } from 'lucide-react';

export type ConversionStep = 1 | 2 | 3 | 4 | 5 | 6;
export function ConversionSteps({
  current,
  onNavigate,
  disabled = false,
  extractMode = false,
  accountingMode = false,
}: {
  current: ConversionStep;
  onNavigate?: (step: ConversionStep) => void;
  disabled?: boolean;
  extractMode?: boolean;
  accountingMode?: boolean;
}) {
  return (
    <ol className="conversion-flow-steps" aria-label="Étapes de la conversion">
      {(accountingMode
        ? [
            extractMode ? 'Importer l’extrait' : 'Importer le relevé',
            'Choisir le modèle',
            'Vérifier le résultat',
            'Télécharger',
          ]
        : extractMode
          ? ['Importer le PDF', 'Choisir le modèle', 'Traiter et filtrer', 'Télécharger']
          : ['Importer le relevé', 'Choisir le modèle', 'Vérifier le résultat', 'Télécharger']
      ).map((label, index) => (
        <li
          key={label}
          className={index + 1 === current ? 'current' : index + 1 < current ? 'complete' : ''}
          aria-current={index + 1 === current ? 'step' : undefined}
        >
          {onNavigate && index + 1 < current ? (
            <button
              type="button"
              aria-label={label}
              disabled={disabled}
              onClick={() => onNavigate((index + 1) as ConversionStep)}
            >
              <span>
                <Check size={14} />
              </span>
              {label}
            </button>
          ) : (
            <>
              <span>{index + 1 < current ? <Check size={14} /> : index + 1}</span>
              {label}
            </>
          )}
        </li>
      ))}
    </ol>
  );
}
