(() => {
  try {
    const script = document.currentScript || (() => {
      const scripts = document.getElementsByTagName('script');
      return scripts[scripts.length - 1] || null;
    })();

    if (!script) {
      return;
    }

    const siteKey = script.getAttribute('data-site-key');
    if (!siteKey) {
      return;
    }

    const src = script.getAttribute('src') || '';
    if (!src) {
      return;
    }

    const baseUrl = new URL(src, window.location.href).origin;
    const payload = {
      siteKey,
      type: 'pageview',
      url: window.location.href,
      referrer: document.referrer || null,
      tsUtc: new Date().toISOString()
    };

    const body = JSON.stringify(payload);
    fetch(`${baseUrl}/collector/events`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body,
      keepalive: true
    }).catch(() => undefined);
  } catch {
    // swallow
  }
})();
