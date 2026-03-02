import { createCard } from '../shared/ui/index.js';

export const renderAdsView = (container) => {
  const body = document.createElement('div');
  body.textContent = 'Ads dashboard coming soon.';
  body.style.color = '#475569';

  const card = createCard({
    title: 'Ads',
    body,
  });
  card.style.maxWidth = '720px';
  card.style.width = '100%';

  container.appendChild(card);
};
