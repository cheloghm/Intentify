(function () {
  var STORAGE_KEY = 'intentify_consent';
  var GRANTED = 'granted';
  var DENIED = 'denied';

  var scriptTag = document.currentScript;

  // Configurable via data-* attributes on the script tag
  var cfg = {
    title:       (scriptTag && scriptTag.getAttribute('data-title'))        || 'We value your privacy',
    body:        (scriptTag && scriptTag.getAttribute('data-body'))         || 'We use cookies and similar technologies to identify visitors, improve your experience, and serve relevant content. You can accept or decline below.',
    acceptLabel: (scriptTag && scriptTag.getAttribute('data-accept-label')) || 'Accept',
    declineLabel:(scriptTag && scriptTag.getAttribute('data-decline-label'))|| 'Decline',
    privacyUrl:  (scriptTag && scriptTag.getAttribute('data-privacy-url'))  || '',
    privacyLabel:(scriptTag && scriptTag.getAttribute('data-privacy-label'))|| 'Privacy Policy',
    position:    (scriptTag && scriptTag.getAttribute('data-position'))     || 'bottom', // 'bottom' | 'top'
    primaryColor:(scriptTag && scriptTag.getAttribute('data-primary-color'))|| '#2563eb',
  };

  // ── Consent state helpers ────────────────────────────────────────────────

  function readConsent() {
    try { return localStorage.getItem(STORAGE_KEY); } catch (e) { return null; }
  }

  function writeConsent(value) {
    try { localStorage.setItem(STORAGE_KEY, value); } catch (e) {}
    // Mirror to a cookie so server-side code can read it
    var days = 365;
    var ms = days * 24 * 60 * 60;
    var expires = new Date(Date.now() + ms * 1000).toUTCString();
    var secure = window.location.protocol === 'https:' ? '; Secure' : '';
    document.cookie = STORAGE_KEY + '=' + encodeURIComponent(value) +
      '; Expires=' + expires + '; Max-Age=' + ms + '; Path=/; SameSite=Lax' + secure;
  }

  // ── Callback registry ────────────────────────────────────────────────────

  var consentCallbacks = [];
  var denyCallbacks = [];

  function fireCallbacks(list) {
    for (var i = 0; i < list.length; i++) {
      try { list[i](); } catch (e) {}
    }
  }

  // ── Public API ───────────────────────────────────────────────────────────
  // window.IntentifyConsent is checked by tracker.js before firing events.

  window.IntentifyConsent = {
    hasConsent: function () { return readConsent() === GRANTED; },
    isDenied:   function () { return readConsent() === DENIED; },
    isPending:  function () { var v = readConsent(); return v !== GRANTED && v !== DENIED; },
    grant: function () {
      writeConsent(GRANTED);
      removeBanner();
      fireCallbacks(consentCallbacks);
    },
    deny: function () {
      writeConsent(DENIED);
      removeBanner();
      fireCallbacks(denyCallbacks);
    },
    onConsent: function (fn) {
      if (typeof fn === 'function') {
        if (readConsent() === GRANTED) { try { fn(); } catch (e) {} }
        else { consentCallbacks.push(fn); }
      }
    },
    onDeny: function (fn) {
      if (typeof fn === 'function') {
        if (readConsent() === DENIED) { try { fn(); } catch (e) {} }
        else { denyCallbacks.push(fn); }
      }
    },
    reset: function () {
      try { localStorage.removeItem(STORAGE_KEY); } catch (e) {}
      document.cookie = STORAGE_KEY + '=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/';
      showBanner();
    },
  };

  // ── Banner DOM ───────────────────────────────────────────────────────────

  var bannerId = 'intentify-gdpr-banner';
  var banner = null;

  function removeBanner() {
    var el = document.getElementById(bannerId);
    if (el) { el.parentNode.removeChild(el); }
    banner = null;
  }

  function hexToRgba(hex, alpha) {
    var r = parseInt(hex.slice(1, 3), 16);
    var g = parseInt(hex.slice(3, 5), 16);
    var b = parseInt(hex.slice(5, 7), 16);
    return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
  }

  function showBanner() {
    if (document.getElementById(bannerId)) { return; }

    var isTop = cfg.position === 'top';
    var primary = cfg.primaryColor;

    banner = document.createElement('div');
    banner.id = bannerId;
    banner.setAttribute('role', 'dialog');
    banner.setAttribute('aria-modal', 'false');
    banner.setAttribute('aria-label', cfg.title);

    var bannerStyle = [
      'position:fixed',
      isTop ? 'top:0' : 'bottom:0',
      'left:0',
      'right:0',
      'z-index:2147483647',
      'background:#1e293b',
      'color:#f1f5f9',
      'font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif',
      'font-size:14px',
      'line-height:1.5',
      'padding:16px 20px',
      'box-shadow:0 ' + (isTop ? '2px 12px' : '-2px 12px') + ' 0 rgba(0,0,0,0.3)',
      'display:flex',
      'align-items:center',
      'gap:16px',
      'flex-wrap:wrap',
    ].join(';');
    banner.style.cssText = bannerStyle;

    // Text block
    var textBlock = document.createElement('div');
    textBlock.style.cssText = 'flex:1;min-width:200px';

    var titleEl = document.createElement('strong');
    titleEl.style.cssText = 'display:block;margin-bottom:4px;font-size:15px;color:#f8fafc';
    titleEl.textContent = cfg.title;

    var bodyEl = document.createElement('span');
    bodyEl.style.cssText = 'color:#94a3b8';
    bodyEl.textContent = cfg.body;

    textBlock.appendChild(titleEl);
    textBlock.appendChild(bodyEl);

    if (cfg.privacyUrl) {
      var sep = document.createTextNode(' ');
      var link = document.createElement('a');
      link.href = cfg.privacyUrl;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      link.textContent = cfg.privacyLabel;
      link.style.cssText = 'color:' + primary + ';text-decoration:underline;margin-left:4px';
      textBlock.appendChild(sep);
      textBlock.appendChild(link);
    }

    // Button block
    var btnBlock = document.createElement('div');
    btnBlock.style.cssText = 'display:flex;gap:8px;flex-shrink:0;align-items:center';

    var declineBtn = document.createElement('button');
    declineBtn.type = 'button';
    declineBtn.textContent = cfg.declineLabel;
    declineBtn.style.cssText = [
      'padding:8px 16px',
      'border-radius:6px',
      'border:1px solid #475569',
      'background:transparent',
      'color:#cbd5e1',
      'font-size:13px',
      'font-weight:500',
      'cursor:pointer',
      'white-space:nowrap',
      'transition:background 0.15s',
    ].join(';');
    declineBtn.addEventListener('mouseover', function () {
      declineBtn.style.background = '#334155';
    });
    declineBtn.addEventListener('mouseout', function () {
      declineBtn.style.background = 'transparent';
    });
    declineBtn.addEventListener('click', function () {
      window.IntentifyConsent.deny();
    });

    var acceptBtn = document.createElement('button');
    acceptBtn.type = 'button';
    acceptBtn.textContent = cfg.acceptLabel;
    acceptBtn.style.cssText = [
      'padding:8px 16px',
      'border-radius:6px',
      'border:none',
      'background:' + primary,
      'color:#fff',
      'font-size:13px',
      'font-weight:600',
      'cursor:pointer',
      'white-space:nowrap',
      'transition:background 0.15s',
    ].join(';');
    acceptBtn.addEventListener('mouseover', function () {
      acceptBtn.style.background = hexToRgba(primary, 0.85);
    });
    acceptBtn.addEventListener('mouseout', function () {
      acceptBtn.style.background = primary;
    });
    acceptBtn.addEventListener('click', function () {
      window.IntentifyConsent.grant();
    });

    btnBlock.appendChild(declineBtn);
    btnBlock.appendChild(acceptBtn);

    banner.appendChild(textBlock);
    banner.appendChild(btnBlock);

    // Wait for DOM ready
    if (document.body) {
      document.body.appendChild(banner);
    } else {
      document.addEventListener('DOMContentLoaded', function () {
        document.body.appendChild(banner);
      });
    }
  }

  // ── Init ─────────────────────────────────────────────────────────────────

  var existing = readConsent();
  if (existing !== GRANTED && existing !== DENIED) {
    showBanner();
  }
})();
