import { Check, Plus } from 'lucide-react';
import { useState } from 'react';
import { ModernCircleButton } from './ModernCircleButton';

export type SpotifyCheckButtonProps = {
  checked?: boolean;
  defaultChecked?: boolean;
  onCheckedChange?: (checked: boolean) => void;
  className?: string;
  disabled?: boolean;
};

export function SpotifyCheckButton({
  checked,
  defaultChecked = false,
  onCheckedChange,
  className = '',
  disabled,
}: SpotifyCheckButtonProps) {
  const [internalChecked, setInternalChecked] = useState(defaultChecked);
  const selected = checked ?? internalChecked;

  function toggleChecked() {
    const next = !selected;
    if (checked === undefined) setInternalChecked(next);
    onCheckedChange?.(next);
  }

  return (
    <ModernCircleButton
      aria-pressed={selected}
      className={className}
      disabled={disabled}
      label={selected ? 'Retirer des favoris' : 'Ajouter aux favoris'}
      onClick={toggleChecked}
      size="sm"
    >
      {selected ? (
        <Check aria-hidden="true" size={13} strokeWidth={2.5} />
      ) : (
        <Plus aria-hidden="true" size={13} strokeWidth={2.2} />
      )}
    </ModernCircleButton>
  );
}
