import { createCard } from '../shared/ui/index.js';

export const renderTicketsView = (container) => {
  const body = document.createElement('div');
  body.style.color = '#475569';
  body.style.lineHeight = '1.6';
  body.textContent = 'Tickets (Coming soon)';

  const card = createCard({
    title: 'Tickets',
    body,
  });
  card.style.maxWidth = '640px';
  card.style.width = '100%';
  container.appendChild(card);
};
