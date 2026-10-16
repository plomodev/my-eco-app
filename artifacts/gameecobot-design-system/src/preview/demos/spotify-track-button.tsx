import { useState } from 'react';
import { ModernCard } from '#components/ui/ModernCard';
import { SpotifyTrackButton } from '#components/ui/SpotifyTrackButton';

export function SpotifyTrackButtonDemo() {
  const [track, setTrack] = useState(3);

  return (
    <ModernCard
      eyebrow="Audio · transport"
      title="SpotifyTrackButton"
      description="Commandes précédent/suivant accessibles pour une piste audio."
    >
      <div className="flex items-center gap-4 pt-2">
        <SpotifyTrackButton
          direction="previous"
          onActivate={() => setTrack((value) => Math.max(1, value - 1))}
        />
        <p aria-live="polite" className="min-w-24 text-center text-sm">
          Piste {track}
        </p>
        <SpotifyTrackButton
          direction="next"
          onActivate={() => setTrack((value) => value + 1)}
        />
      </div>
    </ModernCard>
  );
}
