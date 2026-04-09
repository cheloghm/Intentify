(() => {
  try {
    if (navigator.webdriver === true) {
      return;
    }

    const script = document.currentScript || (() => {
      const scripts = document.getElementsByTagName('script');
      return scripts[scripts.length - 1] || null;
    })();

    if (!script) {
      return;
    }

    const siteId = (script.getAttribute('data-site-id') || '').trim();
    const siteKey = siteId || (script.getAttribute('data-site-key') || '').trim();
    if (!siteKey) {
      return;
    }

    const src = script.getAttribute('src') || '';
    if (!src) {
      return;
    }

    const trackerSrc = new URL(src, window.location.href).href;
    const baseUrl = trackerSrc.substring(0, trackerSrc.indexOf('/api/collector/tracker.js'));
    const sessionId = ensureSessionId();
    const visitorId = getOrCreateVisitorId();
    const fingerprint = getBrowserFingerprint();
    const pageLoadedAtMs = Date.now();
    let didSendTimeOnPage = false;
    let maxScrollDepth = 0;
    let exitIntentFired = false;
    var pageMeta = getPageMeta();

    function trimValue(value, max) {
      if (typeof value !== 'string') {
        return null;
      }

      const trimmed = value.trim();
      if (!trimmed) {
        return null;
      }

      return trimmed.length > max ? trimmed.slice(0, max) : trimmed;
    }

    function safeAbsoluteUrl(value, max) {
      if (!value) {
        return null;
      }

      try {
        const absolute = new URL(value, window.location.href).toString();
        return absolute.length > max ? absolute.slice(0, max) : absolute;
      } catch {
        return null;
      }
    }

    function sendEvent(type, data) {
      try {
        const payload = {
          siteKey: siteId ? null : siteKey,
          snippetId: siteId || null,
          type,
          url: window.location.href,
          referrer: document.referrer || null,
          tsUtc: new Date().toISOString(),
          sessionId,
          visitorId,
          fingerprint,
          data: data != null ? Object.assign({}, data, { pageMeta: pageMeta }) : { pageMeta: pageMeta }
        };

        const eventsUrl = siteId
          ? `${baseUrl}/api/collector/events`
          : `${baseUrl}/api/collector/events?siteKey=${encodeURIComponent(siteKey)}`;
        fetch(eventsUrl, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(payload),
          keepalive: true
        }).catch(() => undefined);
      } catch {
        // swallow
      }
    }

    sendEvent('pageview', {
      title: trimValue(document.title, 256)
    });

    document.addEventListener('click', (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }

      const element = target.closest('a,button,input[type="submit"],[role="button"]');
      if (!(element instanceof Element)) {
        return;
      }

      const classList = Array.from(element.classList || []).slice(0, 4).join(' ');
      const href = element.tagName === 'A' ? safeAbsoluteUrl(element.getAttribute('href') || element.href, 2048) : null;
      const outbound = Boolean(href && new URL(href).origin !== window.location.origin);
      const clickData = {
        tag: element.tagName,
        id: trimValue(element.id, 64),
        classes: trimValue(classList, 128),
        text: trimValue(element.innerText || element.textContent || '', 80),
        href,
        outbound
      };

      sendEvent('click', clickData);
      if (outbound) {
        sendEvent('outbound_click', clickData);
      }
    }, true);

    const scrollThresholds = [25, 50, 75, 100];
    const sentThresholds = {};
    let ticking = false;
    function emitScrollDepth() {
      ticking = false;
      const documentElement = document.documentElement;
      const body = document.body;
      const scrollHeight = Math.max(
        documentElement ? documentElement.scrollHeight : 0,
        body ? body.scrollHeight : 0
      );

      const viewportHeight = window.innerHeight || 0;
      const scrollTop = window.scrollY || (documentElement ? documentElement.scrollTop : 0) || 0;
      const denominator = Math.max(scrollHeight - viewportHeight, 1);
      const percent = Math.min(100, Math.round((scrollTop / denominator) * 100));

      maxScrollDepth = Math.max(maxScrollDepth, percent);

      for (let i = 0; i < scrollThresholds.length; i += 1) {
        const threshold = scrollThresholds[i];
        if (percent >= threshold && !sentThresholds[threshold]) {
          sentThresholds[threshold] = true;
          sendEvent('scroll_depth', { percent: threshold });
        }
      }
    }

    window.addEventListener('scroll', () => {
      if (ticking) {
        return;
      }

      ticking = true;
      if (window.requestAnimationFrame) {
        window.requestAnimationFrame(emitScrollDepth);
      } else {
        window.setTimeout(emitScrollDepth, 250);
      }
    }, { passive: true });

    function sendTimeOnPage(reason, useBeacon) {
      if (didSendTimeOnPage) {
        return;
      }

      didSendTimeOnPage = true;
      const seconds = Math.max(0, Math.round((Date.now() - pageLoadedAtMs) / 1000));
      const eventData = { seconds, reason, pageUrl: window.location.href, maxScrollDepth };

      if (useBeacon && navigator.sendBeacon) {
        try {
          const payload = {
            siteKey: siteId ? null : siteKey,
            snippetId: siteId || null,
            type: 'time_on_page',
            url: window.location.href,
            referrer: document.referrer || null,
            tsUtc: new Date().toISOString(),
            sessionId,
            data: eventData,
          };
          const beaconUrl = siteId
            ? `${baseUrl}/api/collector/events`
            : `${baseUrl}/api/collector/events?siteKey=${encodeURIComponent(siteKey)}`;
          navigator.sendBeacon(
            beaconUrl,
            new Blob([JSON.stringify(payload)], { type: 'application/json' })
          );
          return;
        } catch {
          // fall through to sendEvent
        }
      }

      sendEvent('time_on_page', eventData);
    }

    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'hidden') {
        sendTimeOnPage('hidden', false);
      }
    });

    window.addEventListener('pagehide', () => sendTimeOnPage('unload', true));
    window.addEventListener('beforeunload', () => sendTimeOnPage('unload', true));

    // ── Exit intent ────────────────────────────────────────────────────
    document.addEventListener('mousemove', (event) => {
      if (exitIntentFired) {
        return;
      }

      if (event.clientY < 50 && event.movementY < -5) {
        exitIntentFired = true;
        sendEvent('exit_intent', { pageUrl: window.location.href });
      }
    });

    // ── Referral source (once per session) ─────────────────────────────
    try {
      const referralKey = 'intentify_referral_sent';
      if (!window.sessionStorage.getItem(referralKey)) {
        window.sessionStorage.setItem(referralKey, '1');
        const urlParams = new URLSearchParams(window.location.search);
        sendEvent('referral_source', {
          referrer: document.referrer || 'direct',
          utmSource: urlParams.get('utm_source') || null,
          utmMedium: urlParams.get('utm_medium') || null,
          utmCampaign: urlParams.get('utm_campaign') || null,
        });
      }
    } catch {
      // ignore sessionStorage errors
    }

    document.addEventListener('submit', (event) => {
      const form = event.target;
      if (!(form instanceof HTMLFormElement)) {
        return;
      }

      const elements = form.querySelectorAll('input,select,textarea');
      const passwordInputs = form.querySelectorAll('input[type="password"]');
      sendEvent('form_submit', {
        action: safeAbsoluteUrl(form.getAttribute('action') || form.action || null, 2048),
        method: trimValue((form.method || 'GET').toUpperCase(), 16) || 'GET',
        id: trimValue(form.id, 64),
        name: trimValue(form.getAttribute('name') || '', 64),
        fields: elements.length,
        hasPassword: passwordInputs.length > 0
      });
    }, true);

    function ensureSessionId() {
      const cookieName = 'intentify_sid';
      const current = readCookie(cookieName);
      if (current) {
        return current;
      }

      const generated = generateSessionId();
      if (!generated) {
        return '';
      }

      const secureFlag = window.location.protocol === 'https:' ? '; Secure' : '';
      document.cookie = `${cookieName}=${encodeURIComponent(generated)}; Max-Age=1800; Path=/; SameSite=Lax${secureFlag}`;
      return generated;
    }

    function readCookie(name) {
      const escapedName = name.replace(/[-[\]{}()*+?.,\\^$|#\s]/g, '\\$&');
      const match = document.cookie.match(new RegExp(`(?:^|; )${escapedName}=([^;]*)`));
      if (!match) {
        return null;
      }

      try {
        return decodeURIComponent(match[1]);
      } catch {
        return match[1];
      }
    }

    function generateSessionId() {
      if (!window.crypto || !window.crypto.getRandomValues) {
        return null;
      }

      const bytes = new Uint8Array(16);
      window.crypto.getRandomValues(bytes);
      let output = '';
      for (let i = 0; i < bytes.length; i += 1) {
        output += bytes[i].toString(16).padStart(2, '0');
      }

      return output;
    }

    function setCookie(name, value, days) {
      const ms = days * 24 * 60 * 60 * 1000;
      const expires = new Date(Date.now() + ms).toUTCString();
      const secureFlag = window.location.protocol === 'https:' ? '; Secure' : '';
      document.cookie = `${name}=${encodeURIComponent(value)}; Expires=${expires}; Max-Age=${days * 24 * 60 * 60}; Path=/; SameSite=Lax${secureFlag}`;
    }

    function generateUuid() {
      if (window.crypto && typeof window.crypto.randomUUID === 'function') {
        return window.crypto.randomUUID();
      }
      if (window.crypto && window.crypto.getRandomValues) {
        const bytes = new Uint8Array(16);
        window.crypto.getRandomValues(bytes);
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        const hex = Array.from(bytes).map(b => b.toString(16).padStart(2, '0')).join('');
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
      }
      return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
      });
    }

    function getOrCreateVisitorId() {
      let stored = null;
      try { stored = localStorage.getItem('intentify_vid'); } catch (e) {}
      if (!stored) {
        stored = readCookie('intentify_vid');
      }
      if (!stored) {
        stored = generateUuid();
        try { localStorage.setItem('intentify_vid', stored); } catch (e) {}
        setCookie('intentify_vid', stored, 365);
      }
      return stored;
    }

    function getPageMeta() {
      try {
        const og = (prop) => document.querySelector('meta[property="' + prop + '"]')?.content || null;
        const name = (n) => document.querySelector('meta[name="' + n + '"]')?.content || null;
        const itemprop = (p) => document.querySelector('[itemprop="' + p + '"]')?.content || document.querySelector('[itemprop="' + p + '"]')?.innerText || null;

        // Try JSON-LD schema first (most reliable for e-commerce)
        let schemaData = null;
        const ldScripts = document.querySelectorAll('script[type="application/ld+json"]');
        for (var i = 0; i < ldScripts.length; i++) {
          try {
            const parsed = JSON.parse(ldScripts[i].textContent);
            const schema = Array.isArray(parsed) ? parsed[0] : parsed;
            if (schema && (schema['@type'] === 'Product' || schema['@type'] === 'ItemPage')) {
              schemaData = schema;
              break;
            }
          } catch(e) {}
        }

        return {
          ogTitle:          og('og:title') || null,
          ogDescription:    og('og:description') || null,
          ogType:           og('og:type') || null,
          ogImage:          og('og:image') || null,
          productName:      schemaData?.name || itemprop('name') || og('og:title') || null,
          productPrice:     schemaData?.offers?.price || schemaData?.offers?.lowPrice || itemprop('price') || og('product:price:amount') || null,
          productCurrency:  schemaData?.offers?.priceCurrency || itemprop('priceCurrency') || og('product:price:currency') || null,
          productBrand:     schemaData?.brand?.name || itemprop('brand') || null,
          productSku:       schemaData?.sku || itemprop('sku') || null,
          productAvailable: schemaData?.offers?.availability?.includes('InStock') || null,
          productCategory:  schemaData?.category || itemprop('category') || null,
          schemaType:       schemaData?.['@type'] || null,
          pageType:         og('og:type') || null,
        };
      } catch(e) {
        return {};
      }
    }

    function getBrowserFingerprint() {
      const signals = [
        navigator.userAgent,
        navigator.language,
        `${screen.width}x${screen.height}`,
        screen.colorDepth,
        new Date().getTimezoneOffset(),
        navigator.hardwareConcurrency || 0,
        navigator.platform || '',
      ].join('|');
      let hash = 0;
      for (let i = 0; i < signals.length; i += 1) {
        hash = ((hash << 5) - hash) + signals.charCodeAt(i);
        hash |= 0;
      }
      return Math.abs(hash).toString(36);
    }
  } catch {
    // swallow
  }
})();
