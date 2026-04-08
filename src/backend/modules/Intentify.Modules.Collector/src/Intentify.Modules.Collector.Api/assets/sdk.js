(() => {
  const scriptTag = document.currentScript;
  if (!scriptTag) {
    return;
  }

  const siteId = (scriptTag.getAttribute('data-site-id') || '').trim();
  const siteKey = siteId || (scriptTag.getAttribute('data-site-key') || '').trim();
  if (!siteKey) {
    console.error('[Intentify SDK] Missing data-site-id on sdk.js script tag.');
    return;
  }

  const sourceUrl = new URL(scriptTag.src, window.location.href);
  const baseUrl = sourceUrl.origin;

  const loadScript = ({ src, attributes = {} }) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      return;
    }

    const script = document.createElement('script');
    script.async = true;
    script.src = src;

    Object.entries(attributes).forEach(([name, value]) => {
      if (value !== null && value !== undefined && String(value).length > 0) {
        script.setAttribute(name, String(value));
      }
    });

    document.head.appendChild(script);
  };

  fetch(`${baseUrl}/collector/sdk/bootstrap?siteKey=${encodeURIComponent(siteKey)}`)
    .then((response) => {
      if (!response.ok) {
        throw new Error(`SDK bootstrap failed with status ${response.status}`);
      }
      return response.json();
    })
    .then((bootstrap) => {
      const widgetKey = (bootstrap && bootstrap.widgetKey) || '';
      if (!widgetKey) {
        throw new Error('SDK bootstrap did not return a widget key.');
      }

      loadScript({
        src: `${baseUrl}/collector/tracker.js`,
        attributes: siteId
          ? { 'data-site-id': siteId }
          : { 'data-site-key': siteKey },
      });

      // Pass the persistent visitor ID if already stored (returning visitor).
      // New visitors won't have it yet; widget.js reads localStorage at send-time.
      let existingVisitorId = '';
      try { existingVisitorId = localStorage.getItem('intentify_vid') || ''; } catch (e) {}

      loadScript({
        src: `${baseUrl}/engage/widget.js`,
        attributes: { 'data-widget-key': widgetKey, 'data-visitor-id': existingVisitorId },
      });
    })
    .catch((error) => {
      console.error('[Intentify SDK] Unable to initialize.', error);
    });
})();
