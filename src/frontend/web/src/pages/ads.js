import { createCard } from '../shared/ui/index.js';

export const renderAdsView = (container) => {
  const body = document.createElement('div');
  body.style.color = '#475569';
  body.style.lineHeight = '1.6';
  body.textContent = 'Ads dashboard is coming soon.';

  const card = createCard({
    title: 'Ads',
    body,
  });
  card.style.maxWidth = '640px';
  card.style.width = '100%';
  container.appendChild(card);
};
