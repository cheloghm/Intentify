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

    const siteKey = script.getAttribute('data-site-key');
    if (!siteKey) {
      return;
    }

    const src = script.getAttribute('src') || '';
    if (!src) {
      return;
    }

    const baseUrl = new URL(src, window.location.href).origin;
    const sessionId = ensureSessionId();
    const pageLoadedAtMs = Date.now();
    let didSendTimeOnPage = false;

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
          siteKey,
          type,
          url: window.location.href,
          referrer: document.referrer || null,
          tsUtc: new Date().toISOString(),
          sessionId,
          data: data || null
        };

        fetch(`${baseUrl}/collector/events`, {
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

    function sendTimeOnPage(reason) {
      if (didSendTimeOnPage) {
        return;
      }

      didSendTimeOnPage = true;
      const seconds = Math.max(0, Math.round((Date.now() - pageLoadedAtMs) / 1000));
      sendEvent('time_on_page', { seconds, reason });
    }

    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'hidden') {
        sendTimeOnPage('hidden');
      }
    });

    window.addEventListener('pagehide', () => sendTimeOnPage('unload'));
    window.addEventListener('beforeunload', () => sendTimeOnPage('unload'));

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
  } catch {
    // swallow
  }
})();
