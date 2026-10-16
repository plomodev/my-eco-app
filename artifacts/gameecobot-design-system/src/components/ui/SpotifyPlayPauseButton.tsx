import { Pause, Play } from 'lucide-react';
import { useState } from 'react';
import { ModernCircleButton } from './ModernCircleButton';

export type SpotifyPlayPauseButtonProps = {
  isPlaying?: boolean;
  defaultIsPlaying?: boolean;
  onPlayingChange?: (isPlaying: boolean) => void;
  className?: string;
  disabled?: boolean;
};

export function SpotifyPlayPauseButton({
  isPlaying,
  defaultIsPlaying = false,
  onPlayingChange,
  className = '',
  disabled,
}: SpotifyPlayPauseButtonProps) {
  const [internalIsPlaying, setInternalIsPlaying] = useState(defaultIsPlaying);
  const playing = isPlaying ?? internalIsPlaying;

  function togglePlayback() {
    const next = !playing;
    if (isPlaying === undefined) setInternalIsPlaying(next);
    onPlayingChange?.(next);
  }

  return (
    <ModernCircleButton
      aria-pressed={playing}
      className={`size-[34px] ${className}`}
      disabled={disabled}
      label={playing ? 'Mettre en pause' : 'Lire'}
      onClick={togglePlayback}
      size="md"
    >
      {playing ? (
        <Pause aria-hidden="true" size={14} strokeWidth={2.4} />
      ) : (
        <Play
          aria-hidden="true"
          className="translate-x-px"
          size={14}
          strokeWidth={2.4}
        />
      )}
    </ModernCircleButton>
  );
}
