import { SkipBack, SkipForward } from 'lucide-react';
import { ModernCircleButton } from './ModernCircleButton';

export type SpotifyTrackButtonProps = {
  direction: 'previous' | 'next';
  onActivate?: () => void;
  className?: string;
  disabled?: boolean;
};

export function SpotifyTrackButton({
  direction,
  onActivate,
  className = '',
  disabled,
}: SpotifyTrackButtonProps) {
  const previous = direction === 'previous';

  return (
    <ModernCircleButton
      className={className}
      disabled={disabled}
      label={previous ? 'Piste précédente' : 'Piste suivante'}
      onClick={onActivate}
      size="sm"
    >
      {previous ? (
        <SkipBack aria-hidden="true" size={14} strokeWidth={2.2} />
      ) : (
        <SkipForward aria-hidden="true" size={14} strokeWidth={2.2} />
      )}
    </ModernCircleButton>
  );
}
