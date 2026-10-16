import { lazy, type ComponentType } from 'react';
import {
  BrandLogoPage,
  ColorsPage,
  FontsPage,
  LayoutPage,
  MotionPage,
  OverviewPage,
} from './foundations';

function lazyPage(load: () => Promise<ComponentType>) {
  return lazy(async () => ({ default: await load() }));
}

const ModernCardDemo = lazyPage(() =>
  import('./demos/modern-card').then(({ ModernCardDemo }) => ModernCardDemo),
);
const ModernButtonDemo = lazyPage(() =>
  import('./demos/modern-button').then(({ ModernButtonDemo }) => ModernButtonDemo),
);
const ModernCircleButtonDemo = lazyPage(() =>
  import('./demos/modern-circle-button').then(
    ({ ModernCircleButtonDemo }) => ModernCircleButtonDemo,
  ),
);
const ModernToggleSwitchDemo = lazyPage(() =>
  import('./demos/modern-toggle-switch').then(
    ({ ModernToggleSwitchDemo }) => ModernToggleSwitchDemo,
  ),
);
const ModernSliderDemo = lazyPage(() =>
  import('./demos/modern-slider').then(({ ModernSliderDemo }) => ModernSliderDemo),
);
const HardwareGraphControlDemo = lazyPage(() =>
  import('./demos/hardware-graph-control').then(
    ({ HardwareGraphControlDemo }) => HardwareGraphControlDemo,
  ),
);
const SpotifyPlayPauseButtonDemo = lazyPage(() =>
  import('./demos/spotify-play-pause-button').then(
    ({ SpotifyPlayPauseButtonDemo }) => SpotifyPlayPauseButtonDemo,
  ),
);
const SpotifyTrackButtonDemo = lazyPage(() =>
  import('./demos/spotify-track-button').then(
    ({ SpotifyTrackButtonDemo }) => SpotifyTrackButtonDemo,
  ),
);
const SpotifyCheckButtonDemo = lazyPage(() =>
  import('./demos/spotify-check-button').then(
    ({ SpotifyCheckButtonDemo }) => SpotifyCheckButtonDemo,
  ),
);

export type PreviewEntry = {
  id: string;
  name: string;
  description: string;
  Page: ComponentType;
};

export type NavGroup = {
  name: string;
  entries: PreviewEntry[];
};

export const DESIGN_SYSTEM = {
  title: 'GameEcoBot Design System',
  description:
    'Un tableau de gestion PC avec CPU, RAM, GPU et jeux installés localement, dans le style violet de GameEcoBot.',
} as const;

export const OVERVIEW_ENTRY: PreviewEntry = {
  id: 'overview',
  name: 'Vue d’ensemble',
  description: 'Palette, formes et composants essentiels de GameEcoBot.',
  Page: OverviewPage,
};

export const NAV_GROUPS: NavGroup[] = [
  {
    name: 'Marque',
    entries: [
      {
        id: 'brand-logo',
        name: 'Icône',
        description: 'Icône originale de GameEcoBot.',
        Page: BrandLogoPage,
      },
    ],
  },
  {
    name: 'Couleurs',
    entries: [
      {
        id: 'color-roles',
        name: 'Palette',
        description: 'Rôles violets, neutres et sémantiques.',
        Page: ColorsPage,
      },
    ],
  },
  {
    name: 'Typographie',
    entries: [
      {
        id: 'type-scale',
        name: 'Échelle typographique',
        description: 'Familles et hiérarchie de texte.',
        Page: FontsPage,
      },
    ],
  },
  {
    name: 'Mise en page',
    entries: [
      {
        id: 'spacing-radius',
        name: 'Espacement et relief',
        description: 'Rythme compact, rayons et élévation des surfaces.',
        Page: LayoutPage,
      },
    ],
  },
  {
    name: 'Surfaces',
    entries: [
      {
        id: 'modern-card',
        name: 'ModernCard',
        description: 'Surfaces et panneaux pour les mesures PC.',
        Page: ModernCardDemo,
      },
    ],
  },
  {
    name: 'Graphiques',
    entries: [
      {
        id: 'hardware-graph-control',
        name: 'HardwareGraphControl',
        description: 'Historique CPU, RAM et GPU avec états de disponibilité.',
        Page: HardwareGraphControlDemo,
      },
    ],
  },
  {
    name: 'Actions',
    entries: [
      {
        id: 'modern-button',
        name: 'ModernButton',
        description: 'Boutons à états visuels nets et profondeur pressable.',
        Page: ModernButtonDemo,
      },
      {
        id: 'modern-circle-button',
        name: 'ModernCircleButton',
        description: 'Actions circulaires accessibles.',
        Page: ModernCircleButtonDemo,
      },
    ],
  },
  {
    name: 'Audio',
    entries: [
      {
        id: 'spotify-play-pause-button',
        name: 'SpotifyPlayPauseButton',
        description: 'Commande lecture/pause pour la lecture audio.',
        Page: SpotifyPlayPauseButtonDemo,
      },
      {
        id: 'spotify-track-button',
        name: 'SpotifyTrackButton',
        description: 'Commandes piste précédente et suivante.',
        Page: SpotifyTrackButtonDemo,
      },
      {
        id: 'spotify-check-button',
        name: 'SpotifyCheckButton',
        description: 'État enregistré d’une piste dans la bibliothèque.',
        Page: SpotifyCheckButtonDemo,
      },
    ],
  },
  {
    name: 'Réglages',
    entries: [
      {
        id: 'modern-toggle-switch',
        name: 'ModernToggleSwitch',
        description: 'Bascule binaire pour les préférences.',
        Page: ModernToggleSwitchDemo,
      },
      {
        id: 'modern-slider',
        name: 'ModernSlider',
        description: 'Curseur de précision avec valeur visible.',
        Page: ModernSliderDemo,
      },
    ],
  },
  {
    name: 'Mouvement',
    entries: [
      {
        id: 'motion-depth',
        name: 'Relief et mouvement',
        description: 'États de survol, pression et accessibilité.',
        Page: MotionPage,
      },
    ],
  },
];

export const ALL_ENTRIES: PreviewEntry[] = [
  OVERVIEW_ENTRY,
  ...NAV_GROUPS.flatMap((group) => group.entries),
];

const duplicateIds = ALL_ENTRIES.map((entry) => entry.id).filter(
  (id, index, ids) => ids.indexOf(id) !== index,
);

if (duplicateIds.length > 0) {
  throw new Error(
    `Duplicate preview page id(s): ${[...new Set(duplicateIds)].join(', ')}`,
  );
}
