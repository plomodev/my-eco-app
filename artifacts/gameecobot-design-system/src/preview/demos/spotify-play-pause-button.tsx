import { useState } from 'react';
import { ModernCard } from '#components/ui/ModernCard';
import { SpotifyPlayPauseButton } from '#components/ui/SpotifyPlayPauseButton';

export function SpotifyPlayPauseButtonDemo() {
  const [isPlaying, setIsPlaying] = useState(false);

  return (
    <ModernCard
      eyebrow="Audio · transport"
      title="SpotifyPlayPauseButton"
      description="Contrôle de lecture avec état accessible et relief interactif."
    >
      <div className="flex items-center gap-4 pt-2">
        <SpotifyPlayPauseButton
          isPlaying={isPlaying}
          onPlayingChange={setIsPlaying}
        />
        <p className="text-sm text-muted-foreground">
          {isPlaying ? 'Lecture en cours' : 'En pause'}
        </p>
      </div>
    </ModernCard>
  );
}
