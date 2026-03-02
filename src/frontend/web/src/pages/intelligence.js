import { createCard } from '../shared/ui/index.js';

export const renderIntelligenceView = (container) => {
  const body = document.createElement('div');
  body.textContent = 'Intelligence dashboard coming soon.';
  body.style.color = '#475569';

  const card = createCard({
    title: 'Intelligence',
    body,
  });
  card.style.maxWidth = '720px';
  card.style.width = '100%';

  container.appendChild(card);
};
