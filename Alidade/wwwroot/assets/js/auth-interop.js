window.authInterop = {
  _popup: null,
  _dotnetRef: null,
  _verifier: null,
  _expectedState: null,
  _messageHandler: null,

  openLoginPopup(authUrl, dotnetRef, verifier, state) {
    this._dotnetRef = dotnetRef;
    this._verifier = verifier;
    this._expectedState = state;

    // Remove any leftover listener from a previous aborted flow.
    if (this._messageHandler) {
      window.removeEventListener('message', this._messageHandler);
    }

    // Store the bound reference so we can remove it precisely once the OAuth
    // callback is processed. Using { once: true } is a bug: any unrelated
    // postMessage event (browser extension, Blazor internals, etc.) would
    // silently consume the listener before the OAuth code arrives.
    this._messageHandler = this._onMessage.bind(this);
    window.addEventListener('message', this._messageHandler);

    const width = 600, height = 700;
    const left = Math.max(0, (screen.width - width) / 2);
    const top = Math.max(0, (screen.height - height) / 2);
    this._popup = window.open(
      authUrl, 'osm_oauth',
      `width=${width},height=${height},left=${left},top=${top},toolbar=no,menubar=no`
    );

    this._startLocalStoragePoll();
  },

  _startLocalStoragePoll() {
    if (this._pollTimer) clearInterval(this._pollTimer);
    const deadline = Date.now() + 3 * 60 * 1000;
    this._pollTimer = setInterval(() => {
      if (Date.now() > deadline) {
        console.warn('[auth] localStorage poll timed out');
        clearInterval(this._pollTimer);
        this._pollTimer = null;
        return;
      }
      const raw = localStorage.getItem('oauth_callback_code');
      if (!raw) return;
      try {
        const payload = JSON.parse(raw);
        if (!payload.code) return;
        localStorage.removeItem('oauth_callback_code');
        clearInterval(this._pollTimer);
        this._pollTimer = null;
        if (this._messageHandler) {
          window.removeEventListener('message', this._messageHandler);
          this._messageHandler = null;
        }

        this._dotnetRef.invokeMethodAsync('OnOAuthCallbackAsync', payload.code, this._verifier)
          .then(() => console.log('[auth] OnOAuthCallbackAsync completed (localStorage path)'))
          .catch(err => console.error('[auth] OnOAuthCallbackAsync failed (localStorage path):', err));
      } catch (e) {
        console.error('[auth] localStorage parse error:', e);
      }
    }, 500);
  },

  _onMessage(event) {
    if (event.origin !== window.location.origin) {
      return;
    }
    const data = event.data;
    if (!data || data.type !== 'oauth_callback') {
      return;
    }

    // Confirmed OAuth callback, remove the listener now.
    window.removeEventListener('message', this._messageHandler);
    this._messageHandler = null;

    if (this._popup && !this._popup.closed) this._popup.close();

    if (data.error) {
      console.error('[auth] OAuth error from callback:', data.error);
      return;
    }
    if (!data.code) {
      console.error('[auth] callback message has no code');
      return;
    }

    this._dotnetRef.invokeMethodAsync('OnOAuthCallbackAsync', data.code, this._verifier)
      .then(() => console.log('[auth] OnOAuthCallbackAsync completed'))
      .catch(err => console.error('[auth] OnOAuthCallbackAsync failed:', err));
  }
};
