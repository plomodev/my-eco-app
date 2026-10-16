import { useState } from 'react';
import { ModernCard } from '#components/ui/ModernCard';
import { SpotifyCheckButton } from '#components/ui/SpotifyCheckButton';

export function SpotifyCheckButtonDemo() {
  const [saved, setSaved] = useState(false);

  return (
    <ModernCard
      eyebrow="Audio · bibliothèque"
      title="SpotifyCheckButton"
      description="État enregistré/non enregistré avec retour accessible."
    >
      <div className="flex items-center gap-3 pt-2">
        <SpotifyCheckButton checked={saved} onCheckedChange={setSaved} />
        <p aria-live="polite" className="text-sm text-muted-foreground">
          {saved ? 'Ajouté à la bibliothèque' : 'Pas encore enregistré'}
        </p>
      </div>
    </ModernCard>
  );
}
