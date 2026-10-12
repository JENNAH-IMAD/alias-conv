import { Check } from 'lucide-react';

export type ConversionStep = 1 | 2 | 3 | 4;
export function ConversionSteps({
  current,
  onNavigate,
  disabled = false,
}: {
  current: ConversionStep;
  onNavigate?: (step: ConversionStep) => void;
  disabled?: boolean;
}) {
  return (
    <ol className="conversion-flow-steps" aria-label="Étapes de la conversion">
      {['Importer le relevé', 'Choisir le modèle', 'Vérifier le résultat', 'Télécharger'].map(
        (label, index) => (
          <li
            key={label}
            className={index + 1 === current ? 'current' : index + 1 < current ? 'complete' : ''}
            aria-current={index + 1 === current ? 'step' : undefined}
          >
            {onNavigate && index + 1 < current ? (
              <button
                type="button"
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
        ),
      )}
    </ol>
  );
}
